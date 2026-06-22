# bancho.py analysis done by claude code

---

## 1. Architecture

Two channels, both served over HTTP to `*.ppy.sh` subdomains:

| Channel | Subdomain | Protocol | Purpose |
|---------|-----------|----------|---------|
| Bancho | `c.ppy.sh`, `c4.ppy.sh`—`c6.ppy.sh` | Binary packets over HTTP POST | Real-time: login, presence, chat, multiplayer, spectating |
| osu! web | `osu.ppy.sh` | HTTP GET/POST (form data) | Scores, leaderboards, beatmap search, screenshots, social |

The client first POSTs to `c.ppy.sh` for login (no token → login flow), gets back a session token, then uses that token on subsequent bancho POSTs. Web API calls use username + password hash in query/form params.

### Auth summary

| Method | Where | Format |
|--------|-------|--------|
| `osu-token` header | Bancho POST / | Session token from login response |
| `u` + `h` query params | Most osu! web GETs | username + md5(password) |
| `u` + `p` form/query | Some osu! web endpoints | username + md5(password) |

### Response formats

| Format | Used by |
|--------|---------|
| Binary (little-endian, `[packet_id:u16][_:u8][len:u32][data]`) | Bancho protocol |
| Pipe-delimited text (`key\|value\|...\n`) | osu! web API (scores, beatmaps, search) |
| Empty body (`""`) | Several osu! web endpoints |
| Binary file | Replays (`.osr`), screenshots |

---

## 2. Bancho — login

**Endpoint:** `POST /` to `c.ppy.sh` (or `c4.ppy.sh`—`c6.ppy.sh`)
**Header:** `osu-token` — if absent at all → login flow triggered
**Header:** `user-agent: osu!` (required)

### Client sends (raw body, newline-delimited)

```
username\n
password_md5\n
osu_version|utc_offset|display_city|client_hashes|pm_private\n
```

Where:
- `osu_version` — e.g. `b20240101.2cuttingedge` (stream + date)
- `client_hashes` — 5 MD5s pipe-delimited: `osu_path|adapters|uninstall_id|disk_signature|unused`
- `utc_offset` — signed int (e.g. `+3`)
- `display_city` — bool (`True`/`False`)
- `pm_private` — bool (`0`/`1`), "block non-friend DMs"

### Server responds

On success, HTTP `200` with:
- **Header:** `cho-token: <random_token>` — session token, client sends this back as `osu-token`
- **Body:** sequence of binary packets (concatenated):

| Order | Packet | ID | Content |
|-------|--------|----|---------|
| 1 | `PROTOCOL_VERSION` | 75 | `i32` — server protocol version (e.g. `19`) |
| 2 | `USER_ID` (login reply) | 5 | `i32` — user ID (positive = success) |
| 3 | `PRIVILEGES` | 71 | `i32` — bitfield of client privileges |
| 4 | `NOTIFICATION` | 24 | `string` — welcome message |
| 5 | `CHANNEL_INFO` (×N) | 65 | `string name`, `string topic`, `i32 player_count` — for each auto-join channel the player can read |
| 6 | `CHANNEL_INFO_END` | 89 | (no data) — signals end of channel list |
| 7 | `MAIN_MENU_ICON` | 76 | `string` — `icon_url\|onclick_url` |
| 8 | `FRIENDS_LIST` | 72 | `i32_list` — list of friend user IDs |
| 9 | `SILENCE_END` | 92 | `i32` — remaining silence seconds (0 if not silenced) |
| 10 | `USER_PRESENCE` (self) | 83 | own presence data |
| 11 | `USER_STATS` (self) | 11 | own stats data |
| 12 | `USER_PRESENCE` + `USER_STATS` for each online player | 83+11 | all other online players' data |
| 13 | `SEND_MESSAGE` (×N) | 7 | offline mail messages, if any |
| 14 | `ACCOUNT_RESTRICTED` | 104 | (restricted only) + restriction message |

On failure, HTTP `200` but body contains `USER_ID` with a negative value:

| Code | Enum | Meaning |
|------|------|---------|
| `-1` | `AUTHENTICATION_FAILED` | Bad credentials or other error (+ notification message) |
| `-2` | `OLD_CLIENT` | Client version too old (+ `VERSION_UPDATE` packet) |
| `-3` | `BANNED` | Account banned |
| `-5` | `ERROR_OCCURRED` | Internal error |
| `-6` | `NEEDS_SUPPORTER` | Supporter required |
| `-7` | `PASSWORD_RESET` | Password reset required |
| `-8` | `REQUIRES_VERIFICATION` | Account needs verification |

---

## 3. Bancho — client packets (Client → Server)

Format on wire: `[packet_id:u16][padding:u8][data_len:u32][data:bytes]`. Multiple packets can be concatenated in one POST body.

### 3.1 Presence & status

#### PING — ID 4
- **Data:** (none)
- **Response:** (none) — keeps the connection alive, resets idle timer

#### CHANGE_ACTION — ID 0
- **Data:** `u8 action`, `string info_text`, `string map_md5`, `u32 mods`, `u8 mode`, `i32 map_id`
- **Response:** `USER_STATS(11)` broadcast to all online players
  - mode is adjusted: if `RELAX` mod set → mode += 4; if `AUTOPILOT` mod set → mode += 8
- **Action IDs:** 0=Idle, 1=Afk, 2=Playing, 3=Editing, 4=Modding, 5=Multiplayer, 6=Watching, 7=Unknown, 8=Testing, 9=Submitting, 10=Paused, 11=Lobby, 12=Multiplaying, 13=OsuDirect

#### REQUEST_STATUS_UPDATE — ID 3
- **Data:** (none)
- **Response:** `USER_STATS(11)` sent back to the requesting player

#### RECEIVE_UPDATES — ID 79
- **Data:** `i32 value` — presence filter: 0=Nil (none), 1=All, 2=Friends
- **Response:** (none) — updates internal filter, affects which presence packets the client receives

#### SET_AWAY_MESSAGE — ID 82
- **Data:** `message` (full `Message` struct: `string sender`, `string text`, `string recipient`, `i32 sender_id`)
- **Response:** (none) — stores away message; DMing this player will autorespond with it

### 3.2 Chat & messaging

#### SEND_PUBLIC_MESSAGE — ID 1
- **Data:** `message` (`string sender`, `string text`, `string recipient`, `i32 sender_id`)
- `recipient` = channel name (e.g. `#osu`, `#multiplayer`, `#spectator`)
- **Response:** `SEND_MESSAGE(7)` broadcast to channel members (including sender)
  - Server may also send bot command responses as `SEND_MESSAGE`
  - Silenced players get `USER_SILENCED(94)` instead
  - Messages are truncated at 2000 characters
  - If text matches `/np` pattern, server parses beatmap ID/mode/mods and stores on `player.last_np`

#### SEND_PRIVATE_MESSAGE — ID 25
- **Data:** `message` (`string sender`, `string text`, `string recipient`, `i32 sender_id`)
- **Response (success):** `SEND_MESSAGE(7)` forwarded to target
- **Response (target offline):** message stored as mail in DB, no immediate packet
- **Response (target blocked DMs):** `USER_DM_BLOCKED(100)` to sender
- **Response (target silenced):** `TARGET_IS_SILENCED(101)` to sender
- **Response (target AFK):** `SEND_MESSAGE(7)` with away message auto-reply
- Messages truncated at 2000 characters

### 3.3 Channels

#### CHANNEL_JOIN — ID 63
- **Data:** `string channel_name` (e.g. `#osu`)
- **Response:** `CHANNEL_JOIN_SUCCESS(64)` to joiner, `CHANNEL_INFO(65)` to all channel members (updated player count)
- `#highlight` and `#userlog` are ignored (client-internal channels)

#### CHANNEL_PART — ID 78
- **Data:** `string channel_name`
- **Response:** (none direct — player removed from channel; other members don't get explicit notification, but player count in `CHANNEL_INFO` updates on next join by someone)

### 3.4 Friends

#### FRIEND_ADD — ID 73
- **Data:** `i32 user_id`
- **Response:** (none) — DB + cache update only

#### FRIEND_REMOVE — ID 74
- **Data:** `i32 user_id`
- **Response:** (none) — DB + cache update only

### 3.5 User info queries

#### USER_STATS_REQUEST — ID 85
- **Data:** `i32_list_i16l` — list of user IDs (preceded by `i16` count)
- **Response:** `USER_STATS(11)` for each requested online+unrestricted user (not self). Uses `bot_stats` optimization for bot.

#### USER_PRESENCE_REQUEST — ID 97
- **Data:** `i32_list_i16l` — list of user IDs
- **Response:** `USER_PRESENCE(83)` for each requested user. Uses `bot_presence` optimization for bot.

#### USER_PRESENCE_REQUEST_ALL — ID 98
- **Data:** `i32 ingame_time` — client's current in-game time in seconds
- **Response:** Batch of `USER_PRESENCE(83)` for ALL unrestricted online players (used when >256 players are visible)

### 3.6 Spectating

#### START_SPECTATING — ID 16
- **Data:** `i32 target_id`
- **Response:** `SPECTATOR_JOINED(13)` to host, `FELLOW_SPECTATOR_JOINED(42)` to other spectators
- If re-spectating after downloading a previously-missing map, triggers a spectator-join sequence

#### STOP_SPECTATING — ID 17
- **Data:** (none)
- **Response:** `SPECTATOR_LEFT(14)` to host, `FELLOW_SPECTATOR_LEFT(43)` to other spectators

#### SPECTATE_FRAMES — ID 18
- **Data:** `replay_frame_bundle` (replay frames + score frame, binary)
- **Response:** `SPECTATE_FRAMES(15)` — raw frame data forwarded to all spectators. This is a fast-path (data not parsed, just rewrapped with packet header)

#### CANT_SPECTATE — ID 21
- **Data:** (none)
- **Response:** `SPECTATOR_CANT_SPECTATE(22)` to host + fellow spectators

### 3.7 Multiplayer

Multiplayer packets share these common types:

**Match struct** (`osuTypes.match`):
```
i16 match_id, i8 in_progress, i8 match_type, i16 active_mods, i8 game_mode,
string match_name, string password, string beatmap_name, i32 beatmap_id,
string beatmap_md5, i8 slot_status[N], i8 slot_team[N], i32 slot_user_id[N] (with i8 slot_count=N), i32 host_id, i8 play_mode, i8 scoring_type, i8 team_type, i8 freemods, i32 slot_mods[N] (if freemods), i32 seed
```

#### CREATE_MATCH — ID 31
- **Data:** `match` (full MultiplayerMatch struct)
- **Response (success):** `MATCH_JOIN_SUCCESS(36)` to creator (host), `NEW_MATCH(27)` broadcast to lobby
- **Response (fail):** `MATCH_JOIN_FAIL(37)` to creator
- Validates: host ID matches player, match name length, player not restricted/silenced, player is not already in another match

#### JOIN_MATCH — ID 32
- **Data:** `i32 match_id`, `string password`
- **Response (success):** `MATCH_JOIN_SUCCESS(36)` to joiner (and lobby)
- **Response (fail):** `MATCH_JOIN_FAIL(37)` to joiner
- Checks: match exists, password matches, not restricted/silenced

#### PART_MATCH — ID 33
- **Data:** (none)
- **Response:** `UPDATE_MATCH(26)` to members. If match empty → `DISPOSE_MATCH(28)` broadcast to lobby

#### MATCH_CHANGE_SLOT — ID 38
- **Data:** `i32 slot_id` (0-15)
- **Response:** `UPDATE_MATCH(26)` to all match members
- Swaps player to the specified slot

#### MATCH_READY — ID 39
- **Data:** (none)
- **Response:** `UPDATE_MATCH(26)` to match members (not lobby)

#### MATCH_NOT_READY — ID 55
- **Data:** (none)
- **Response:** `UPDATE_MATCH(26)` to match members (not lobby)

#### MATCH_LOCK — ID 40
- **Data:** `i32 slot_id`
- **Response:** `UPDATE_MATCH(26)` to match members
- Host only. Toggles slot locked/unlocked. Cannot lock host's own slot.

#### MATCH_CHANGE_SETTINGS — ID 41
- **Data:** `match` (full MultiplayerMatch struct)
- **Response:** `UPDATE_MATCH(26)` to match members
- Host only. Handles: freemods toggle (distributes mods to/from slots), map change (fetches beatmap from DB, clears ready states), team type change, win condition change, name change

#### MATCH_CHANGE_MODS — ID 51
- **Data:** `i32 mods`
- **Response:** `UPDATE_MATCH(26)` to match members
- In freemods: speed-changing mods go on match, other mods go on player's slot. In regular mode: host sets match-wide mods.

#### MATCH_CHANGE_TEAM — ID 77
- **Data:** (none)
- **Response:** `UPDATE_MATCH(26)` to match members (not lobby)
- Toggles player's slot between blue and red team

#### MATCH_CHANGE_PASSWORD — ID 90
- **Data:** `match` (password used from struct)
- **Response:** `UPDATE_MATCH(26)` to match members
- Host only

#### MATCH_START — ID 44
- **Data:** (none)
- **Response:** `MATCH_START(46)` to all match players (host only)

#### MATCH_LOAD_COMPLETE — ID 52
- **Data:** (none)
- **Response:** `MATCH_ALL_PLAYERS_LOADED(53)` to all (when all playing slots have loaded)

#### MATCH_NO_BEATMAP — ID 54
- **Data:** (none)
- **Response:** `UPDATE_MATCH(26)` to match members (not lobby)

#### MATCH_HAS_BEATMAP — ID 59
- **Data:** (none)
- **Response:** `UPDATE_MATCH(26)` to match members (not lobby)

#### MATCH_SKIP_REQUEST — ID 60
- **Data:** (none)
- **Response:** `MATCH_PLAYER_SKIPPED(81)` to all. If all skipped → `MATCH_SKIP(61)` to all.

#### MATCH_SCORE_UPDATE — ID 47
- **Data:** `raw` — binary score data (not parsed, forwarded directly)
- **Response:** `MATCH_SCORE_UPDATE(48)` — raw data rewrapped and sent to match members (not lobby). Fast-path.

#### MATCH_FAILED — ID 56
- **Data:** (none)
- **Response:** `MATCH_PLAYER_FAILED(57)` to match members (not lobby)

#### MATCH_COMPLETE — ID 49
- **Data:** (none)
- **Response:** `MATCH_COMPLETE(58)` + `UPDATE_MATCH(26)` to all match members
- If all players finished: resets slot states, sets `in_progress = false`

#### MATCH_TRANSFER_HOST — ID 70
- **Data:** `i32 slot_id`
- **Response:** `MATCH_TRANSFER_HOST(50)` to new host, `UPDATE_MATCH(26)` to all
- Host only

#### MATCH_INVITE — ID 87
- **Data:** `i32 user_id`
- **Response:** `MATCH_INVITE(88)` to target player — contains `Message` with match embed string

#### TOURNAMENT_MATCH_INFO_REQUEST — ID 93
- **Data:** `i32 match_id`
- **Response:** `UPDATE_MATCH(26)` — match info without password
- Requires DONATOR privilege (tournament client)

#### TOURNAMENT_JOIN_MATCH_CHANNEL — ID 108
- **Data:** `i32 match_id`
- **Response:** Normal channel join flow for match's `#multi_{id}` channel
- Requires DONATOR. Joins match chat channel only (not match slots). Adds to `match.tourney_clients`.

#### TOURNAMENT_LEAVE_MATCH_CHANNEL — ID 109
- **Data:** `i32 match_id`
- **Response:** Normal channel leave flow
- Requires DONATOR. Leaves match chat channel.

### 3.8 Lobby

#### JOIN_LOBBY — ID 30
- **Data:** (none)
- **Response:** `NEW_MATCH(27)` for each active match, sent to the joining player

#### PART_LOBBY — ID 29
- **Data:** (none)
- **Response:** (none) — sets `in_lobby = false`

### 3.9 Other

#### TOGGLE_BLOCK_NON_FRIEND_DMS — ID 99
- **Data:** `i32 value` (0=allow, 1=block)
- **Response:** (none) — updates `pm_private` setting

#### LOGOUT — ID 2
- **Data:** `i32 reserved` (discarded)
- **Response:** `USER_LOGOUT(12)` broadcast to all online players (contains user ID + `u8 0`)

### Unhandled packets
- `ERROR_REPORT` (20) — no handler registered
- `BEATMAP_INFO_REQUEST` (68) — no handler registered
- `IRC_ONLY` (84) — no handler registered

---

## 4. Bancho — server packets (Server → Client)

Complete list of packets the server can send to the client. The client processes these to update its local state.

| ID | Name | Data | When sent |
|----|------|------|-----------|
| 5 | `USER_ID` | `i32 user_id` | Login reply (positive=success, negative=LoginFailureReason) |
| 7 | `SEND_MESSAGE` | `message` (sender, text, recipient, sender_id) | Chat message delivery |
| 8 | `PONG` | (none) | Response to ping (not currently implemented as explicit response) |
| 9 | `HANDLE_IRC_CHANGE_USERNAME` | `string "old>>>>new"` | Deprecated/unused |
| 10 | `HANDLE_IRC_QUIT` | (none — not implemented) | IRC quit notification |
| 11 | `USER_STATS` | `i32 uid, u8 action, string info_text, string map_md5, i32 mods, u8 mode, i32 map_id, i64 ranked_score, f32 acc/100, i32 plays, i64 total_score, i32 global_rank, u16 pp` | User stats update (action change, status update) |
| 12 | `USER_LOGOUT` | `i32 uid, u8 0` | User disconnected |
| 13 | `SPECTATOR_JOINED` | `i32 uid` | Someone started spectating you |
| 14 | `SPECTATOR_LEFT` | `i32 uid` | Spectator left |
| 15 | `SPECTATE_FRAMES` | `raw bytes` | Replay frame data from host |
| 19 | `VERSION_UPDATE` | (none) | Client version too old (triggers update prompt) |
| 22 | `SPECTATOR_CANT_SPECTATE` | `i32 uid` | Spectator can't spectate |
| 23 | `GET_ATTENTION` | (none) | Request client attention (taskbar flash) |
| 24 | `NOTIFICATION` | `string msg` | Popup notification in client |
| 26 | `UPDATE_MATCH` | `match` | Match state changed |
| 27 | `NEW_MATCH` | `match` | New match created (lobby) |
| 28 | `DISPOSE_MATCH` | `i32 match_id` | Match destroyed |
| 34 | `TOGGLE_BLOCK_NON_FRIEND_DMS` | (none) | DM blocking state changed (unused) |
| 36 | `MATCH_JOIN_SUCCESS` | `match` | Successfully joined match |
| 37 | `MATCH_JOIN_FAIL` | (none) | Failed to join match |
| 42 | `FELLOW_SPECTATOR_JOINED` | `i32 uid` | Another spectator joined |
| 43 | `FELLOW_SPECTATOR_LEFT` | `i32 uid` | Another spectator left |
| 45 | `ALL_PLAYERS_LOADED` | (none) | All match players loaded (older) |
| 46 | `MATCH_START` | `match` | Match started |
| 48 | `MATCH_SCORE_UPDATE` | `raw bytes` (score frame) | Live score update during play |
| 50 | `MATCH_TRANSFER_HOST` | (none) | You are now the host |
| 53 | `MATCH_ALL_PLAYERS_LOADED` | (none) | All players finished loading |
| 57 | `MATCH_PLAYER_FAILED` | `i32 slot_id` | Player failed the map |
| 58 | `MATCH_COMPLETE` | (none) | Match finished |
| 61 | `MATCH_SKIP` | (none) | All players skipped intro |
| 64 | `CHANNEL_JOIN_SUCCESS` | `string channel_name` | Successfully joined channel |
| 65 | `CHANNEL_INFO` | `channel` (name, topic, player_count) | Channel metadata update |
| 66 | `CHANNEL_KICK` | `string channel_name` | Kicked from channel (not currently used) |
| 67 | `CHANNEL_AUTO_JOIN` | `channel` (name, topic, player_count) | Auto-joined channel on login |
| 69 | `BEATMAP_INFO_REPLY` | `mapInfoReply` (unimplemented) | Beatmap info response |
| 71 | `PRIVILEGES` | `i32 priv_bitfield` | Client privileges (osu!direct, supporter, etc.) |
| 72 | `FRIENDS_LIST` | `i32_list` (list of user IDs) | Your friends list |
| 75 | `PROTOCOL_VERSION` | `i32 version` | Server protocol version (client must match) |
| 76 | `MAIN_MENU_ICON` | `string "icon_url\|onclick_url"` | Main menu banner image |
| 81 | `MATCH_PLAYER_SKIPPED` | `i32 user_id` | A player skipped intro |
| 83 | `USER_PRESENCE` | `i32 uid, string name, u8 utc_offset+24, u8 country_code, u8 priv\|(mode<<5), f32 longitude, f32 latitude, i32 global_rank` | User presence/online status + location |
| 86 | `RESTART` | `i32 delay_ms` | Server restarting soon |
| 88 | `MATCH_INVITE` | `message` (sender, "Come join...", target, sender_id) | Match invite |
| 89 | `CHANNEL_INFO_END` | (none) | End of channel list (triggers reorder) |
| 91 | `MATCH_CHANGE_PASSWORD` | `string new_password` | Match password changed |
| 92 | `SILENCE_END` | `i32 seconds_remaining` | Your remaining silence duration |
| 94 | `USER_SILENCED` | `i32 user_id` | You're silenced (can't send messages) |
| 100 | `USER_DM_BLOCKED` | `message` (empty, with target name) | Target blocked non-friend DMs |
| 101 | `TARGET_IS_SILENCED` | `message` (empty, with target name) | Target is silenced |
| 102 | `VERSION_UPDATE_FORCED` | (none) | Forced update (unused) |
| 103 | `SWITCH_SERVER` | `i32 idle_time` | Switch to next bancho endpoint if idle |
| 104 | `ACCOUNT_RESTRICTED` | (none) | Your account is restricted |
| 106 | `MATCH_ABORT` | (none) | Match aborted |
| 107 | `SWITCH_TOURNAMENT_SERVER` | `string ip` | Switch to tournament server |

### Packet ordering

The client sends packets by ID ascending. Server responses are enqueued per-player (each player has an outbound bytearray). On subsequent `POST /` calls (with `osu-token` header), the server returns whatever bytes have accumulated in that player's queue since the last request.

---

## 5. osu! web API

All endpoints served from `osu.ppy.sh`. Auth via query params `u`+`h` (username + md5(password)), `u`+`p`, or form fields.

---

### 5.1 Score submission

#### `POST /web/osu-submit-modular-selector.php`
The primary score submission endpoint (newer clients).

**Auth:** `pass` form field (md5(password)). Token header validated through `Player` session.

**Form body (multipart):**

| Field | Type | Description |
|-------|------|-------------|
| `pass` | string | md5(password) |
| `st` | string | Score time (Unix timestamp) |
| `ft` | string | Fail time (Unix timestamp) |
| `x` | string | Exited out (`1` if closed before end) |
| `fs` | string | Visual settings (overlay, skin, etc.) |
| `sbk` | string | Storyboard MD5 hash |
| `iv` | string | Initialization vector for AES decryption |
| `c1` | string | Unique ID string from client |
| `osuver` | string | osu! client version |
| `s` | string | Client hash (for validation) |
| `score` | file+b64 | Base64-encoded score data (AES-encrypted) + replay file |
| `bmk` | string | Beatmap MD5 hash (used for consistency check against score data) |
| `i` | file | FL cheat screenshot (optional, only sent if flashlights differ) |

**Score data** (after AES decryption, pipe-delimited):
```
{map_md5}|{player_name}|{online_checksum}|{n300}|{n100}|{n50}|{ngeki}|{nkatu}|{nmiss}|{score}|{max_combo}|{perfect}|{mods}|...
```

**Response format** (on success):
```
# Ranking chart for beatmap
beatmap_id|beatmap_set_id|map_name
chart{...rankings...}
beatmap_ranking_chart|rank|score|name|user_id|accuracy|max_combo|num300|num100|num50|num_geki|num_katu|num_miss|mods|pp|...
achievements{...achievement data...}
```

**Response (failures):**
| Response | Meaning |
|----------|---------|
| `error: beatmap` | Beatmap not found |
| `error: no` | Other error |
| (empty) | Score rejected (duplicate, etc.) |
| `error: ban` | User auto-restricted (PP cap exceeded, no replay) |

**Processing steps:**
1. Decrypt score data with AES (key = `osu!-scoreburgr---------{osu_ver}` padded to 32)
2. Parse pipe-delimited score fields
3. Validate online checksum (SHA1 of specific score fields + client hash)
4. Validate beatmap MD5 matches `bmk` parameter
5. Check for duplicate scores
6. Calculate PP via `akatsuki-pp-py`
7. Insert score into DB, update player stats (tscore, rscore, pp, acc, plays, rank)
8. Check for achievements
9. Return ranking chart + achievement data

**Relax/Autopilot mode handling:** Mode is shifted — RELAX mod → mode + 4, AUTOPILOT mod → mode + 8. These modes are stored in DB as separate mode indices (modes 4-7 for Relax, 8-11 for Autopilot).

#### `POST /web/osu-submit-modular.php`
Legacy score submission endpoint (older clients, disabled if `DISALLOW_OLD_CLIENTS` is set).

Same as above minus `bmk` field validation.

---

### 5.2 Leaderboards

#### `GET /web/osu-osz2-getscores.php`

**Auth:** `us` (query), `ha` (query) — username + md5(password)

**Query params:**

| Param | Type | Description |
|-------|------|-------------|
| `s` | bool | Requesting from editor/song select (0 or 1) |
| `vv` | int | Leaderboard version |
| `v` | int (0-4) | Leaderboard type: 0=Local, 1=Top, 2=Mods, 3=Friends, 4=Country |
| `c` | string (32) | Beatmap MD5 hash |
| `f` | string | Beatmap filename (for lookup when MD5 unknown) |
| `m` | int (0-3) | Game mode (0=osu, 1=taiko, 2=ctb, 3=mania) |
| `i` | int | Beatmap set ID (-1 if unknown) |
| `mods` | int | Enabled mods bitfield |
| `h` | string | Map package hash |
| `a` | bool | AQN files found flag |

**Response format:**

Status line:
```
{ranked_status}|{has_osz2}|{beatmap_id}|{beatmap_set_id}|{score_count}|{featured_artist_track_id}|{featured_artist_license}|
```
- `has_osz2` — always `false` in bancho.py

Info line:
```
0\n{beatmap_full_name}\n{average_rating}
```

Personal best line (empty if none):
```
{score_id}|{player_name}|{score}|{max_combo}|{n50}|{n100}|{n300}|{nmiss}|{nkatu}|{ngeki}|{perfect}|{mods}|{user_id}|{rank}|{timestamp}|{has_replay}
```

Score lines (one per score, format same as PB):
```
{score_id}|{player_name}|{score}|{max_combo}|{n50}|{n100}|{n300}|{nmiss}|{nkatu}|{ngeki}|{perfect}|{mods}|{user_id}|{rank}|{timestamp}|{has_replay}
```

**Edge cases:**
- Unsubmitted map → `-1|false`
- Map needs update (filename exists but MD5 not cached) → `1|false`
- Non-ranked map → `{status}|false|{bid}|{bsid}|{count}|0|` (no scores listed)

**Relax/Autopilot:** Mode is shifted (as in score submission) before querying.

---

### 5.3 Beatmap info & search

#### `POST /web/osu-getbeatmapinfo.php`

**Auth:** `u` (query), `h` (query) — username + md5(password)

**Form body:** `OsuBeatmapRequestForm`
- `Filenames[]` — list of beatmap filenames to look up
- `Ids[]` — list of beatmap IDs to look up

**Response** (pipe-delimited, one per beatmap):
```
{index}|{beatmap_id}|{beatmap_set_id}|{map_md5}|{ranked_status}|{grade_bitfield}
```
- `grade_bitfield` — user's grades on this map. Bitmask: bit0=SS(osu)/SS+(taiko)/max(ctb)/max(mania), bit1=S(osu/taiko)/300(mania)/max(mania), bit2=A, bit3=B, bit4=C, bit5=D.

#### `GET /web/osu-search.php`

**Auth:** `u` (query), `h` (query)

**Query params:**

| Param | Type | Description |
|-------|------|-------------|
| `r` | int (0-8) | Ranked status filter |
| `q` | string | Search query (title, artist, creator, tags) |
| `m` | int (-1 to 3) | Game mode filter (-1 = all) |
| `p` | int | Page number |

**Response** (osu!direct format):
```
{total_results}\n
{beatmap_id}.osz|{artist}|{title}|{creator}|{ranked_status}|{rating}|{download_count}|{-1}|{size}|{length}|{source}|{tags}|{genre}|{language}|{00}|{01}|...
```

#### `GET /web/osu-search-set.php`

**Auth:** `u` (query), `h` (query)

**Query params:**

| Param | Type | Description |
|-------|------|-------------|
| `s` | int | Beatmap set ID |
| `b` | int | Beatmap ID |
| `c` | string (32) | Beatmap checksum (MD5) |

At least one required.

**Response format (pipe-delimited):**
```
{set_id}.osz|{artist}|{title}|{creator}|{ranked_status}|{rating}|{last_updated}|{set_id}|{set_id}|{size}|{osz2_download_available}|{beatmap_id}|{mode}|{version}|{length}|{bpm}|{cs}|{ar}|{od}|{hp}|{sr}|...
```

---

### 5.4 Replays

#### `GET /web/osu-getreplay.php`

**Auth:** `u` (query), `h` (query)

**Query params:**

| Param | Type | Description |
|-------|------|-------------|
| `m` | int (0-3) | Game mode |
| `c` | int | Score ID |

**Response:** Binary `.osr` file (`FileResponse`), or `404` if not found.

---

### 5.5 Beatmap ratings

#### `GET /web/osu-rate.php`

**Auth:** `u` (query), `p` (query) — password hash

**Query params:**

| Param | Type | Description |
|-------|------|-------------|
| `c` | string (32) | Beatmap MD5 |
| `v` | int (1-10) | Rating value (optional — if absent, checks if rateable) |

**Response:**
- `ok` — map can be rated
- `no exist` — map not in cache
- `not ranked` — map not ranked
- `alreadyvoted\n{average}` — already voted or rating just submitted (includes new average)

---

### 5.6 Comments

#### `POST /web/osu-comment.php`

**Auth:** `u` (form), `p` (form)

**Form fields:**

| Field | Type | Description |
|-------|------|-------------|
| `b` | int | Beatmap ID |
| `s` | int | Beatmap set ID |
| `r` | int | Score ID |
| `m` | int (0-3) | Game mode |
| `a` | "get" or "post" | Action |
| `target` | "song" or "map" or "replay" | Comment target (post only) |
| `f` | string (6) | Colour hex (post only) |
| `starttime` | int | Start time in ms (post only) |
| `comment` | string (1-80) | Comment text (post only) |

**Response (get):** Newline-separated comment lines
**Response (post):** Empty body

---

### 5.7 Social

#### `GET /web/osu-getfriends.php`
**Auth:** `u` (query), `h` (query)
**Response:** Newline-separated friend user IDs

#### `GET /web/osu-getfavourites.php`
**Auth:** `u` (query), `h` (query)
**Response:** Newline-separated favourite beatmap set IDs

#### `GET /web/osu-addfavourite.php`
**Auth:** `u` (query), `p` (query)
**Query:** `a` — beatmap set ID
**Response:** `Added favourite!` or `You've already favourited this beatmap!`

#### `GET /web/osu-markasread.php`
**Auth:** `u` (query), `h` (query)
**Query:** `channel` — target username
**Response:** Empty body
**Effect:** Marks all unread mail from that user as read.

---

### 5.8 Screenshots

#### `POST /web/osu-screenshot.php`

**Auth:** `u` (form), `p` (form)

**Form body (multipart):**

| Field | Type | Description |
|-------|------|-------------|
| `v` | int | Version |
| `ss` | file | Screenshot file (PNG/JPEG, max 4MB) |

**Response:** Filename (e.g. `abc12345.jpg`) — 8 random alphanumeric chars + extension.
**Serving:** `GET /ss/{id}.{ext}` (serves from `.data/ss/`)

---

### 5.9 Registration

#### `POST /users`

**Auth:** None (uses `X-Forwarded-For` + `X-Real-IP` for rate limiting)

**Form body:**

| Field | Type | Description |
|-------|------|-------------|
| `user[username]` | string | Desired username |
| `user[user_email]` | string | Email address |
| `user[password]` | string | Password |
| `check` | int | 0 = actual registration, non-zero = pre-check only |

**Validation rules:**
- Username: 2-15 chars, no combined `_` and space, not already taken
- Email: regex validated + unique
- Password: 8-32 chars, at least 3 unique characters, not in disallowed list

**Response (success):** `ok`
**Response (validation error):** JSON with `form_error` dict

**Note:** Can be disabled via `DISALLOW_INGAME_REGISTRATION` setting.

---

### 5.10 Anticheat

#### `GET /web/lastfm.php`

**Auth:** `us` (query), `ha` (query)
**Query:** `action` — `"scrobble"` or `"np"`, `b` — client flag string (starts with anticheat flag letter)

**Response:**
- `-3` — cheating detected (hq!osu or other known cheat flags); also auto-restricts the player
- (empty) — normal

This is the anticheat endpoint. It checks the `b` flag string against known cheat patterns and can auto-restrict players.

---

### 5.11 Client bootstrapping

#### `GET /web/osu-getseasonal.php`
**Auth:** None
**Response:** JSON array of seasonal background objects (configured in server settings). Example:
```json
[{"url": "https://...", "name": "..."}]
```

#### `GET /web/bancho_connect.php`
**Auth:** None
**Query:** `v` (osu_ver), `fail` (endpoint, optional), `fx` (.NET framework, optional), `ch` (client hash, optional), `retry` (bool, optional)
**Response:** Empty body — always success. Client connectivity check before bancho connection.

#### `GET /web/check-updates.php`
**Auth:** None
**Query:** `action` (`"check"`, `"path"`, or `"error"`), `stream` (`"cuttingedge"`, `"stable40"`, `"beta40"`, `"stable"`)
**Response:** Empty body — always responds "no update available".

---

## 6. Binary protocol details

### Wire format

```
[packet_id: u16, little-endian]
[padding: u8]  (always 0)
[data_length: u32, little-endian]  (length of data that follows; 0 if no data)
[data: bytes]  (variable length, structured per packet type)
```

### osuTypes (primitive wire types)

| Name | ID | C type | Size (bytes) |
|------|----|--------|-------|
| `i8` | 0 | int8 | 1 |
| `u8` | 1 | uint8 | 1 |
| `i16` | 2 | int16 | 2 |
| `u16` | 3 | uint16 | 2 |
| `i32` | 4 | int32 | 4 |
| `u32` | 5 | uint32 | 4 |
| `f32` | 6 | float32 | 4 |
| `i64` | 7 | int64 | 8 |
| `u64` | 8 | uint64 | 8 |
| `f64` | 9 | float64 | 8 |
| `message` | 11 | string×3 + i32 | variable |
| `channel` | 12 | string×2 + i32 | variable |
| `match` | 13 | complex struct | variable |
| `scoreframe` | 14 | score frame struct | variable |
| `mapInfoRequest` | 15 | map info request | variable |
| `mapInfoReply` | 16 | map info reply | variable |
| `replayFrameBundle` | 17 | replay frames | variable |
| `i32_list` | 18 | i16 count + i32[] | variable (2-byte length prefix) |
| `i32_list4l` | 19 | i32 count + i32[] | variable (4-byte length prefix) |
| `string` | 20 | u8(11) + ULEB128 length + UTF-8 bytes | variable |
| `raw` | 21 | u32 length + bytes | variable |

### Strings (osuTypes.string, ID 20)
A string is sent as: `u8(11)` (presence byte), followed by a ULEB128-encoded length, then the UTF-8 bytes. Empty strings have length 0 with no bytes following.

### ULEB128 encoding
Variable-length integer encoding used for string lengths. Each byte uses 7 bits for data and the MSB as a continuation flag (1=more bytes follow, 0=last byte).

---

## 7. Data types reference

### Slot status
```
1 << 0  = 1   — open (free slot)
1 << 1  = 2   — locked
1 << 2  = 4   — not ready
1 << 3  = 8   — ready
1 << 7  = 128 — no map
```

### Slot team
```
1 = blue
2 = red
```

### Team types
```
0 = Head to Head
1 = Tag Coop
2 = Team vs
3 = Tag Team vs
```

### Win conditions
```
0 = Score
1 = Accuracy
2 = Combo
3 = Score v2
```

### Ranked status
```
-2 = Graveyard
-1 = WIP
 0 = Pending
 1 = Ranked
 2 = Approved — ranked 
 3 = Qualified
 4 = Loved
```

### Mods (common bitmask values)
```
0           = NoMod
1 << 0      = NF (NoFail)
1 << 1      = EZ (Easy)
1 << 3      = HD (Hidden)
1 << 4      = HR (HardRock)
1 << 5      = SD (SuddenDeath)
1 << 6      = DT (DoubleTime)
1 << 7      = RX (Relax)
1 << 8      = HT (HalfTime)
1 << 9      = NC (NightCore, always paired with DT)
1 << 10     = FL (Flashlight)
1 << 12     = SO (SpunOut)
1 << 14     = PF (Perfect)
1 << 15     = AP (Autopilot)
```

### Client privileges (bits)
```
1 << 0  = Player
1 << 1  = Moderator
1 << 2  = Supporter
1 << 3  = Friend
1 << 4  = Developer
1 << 5  = Tournament
```

### Presence filter
```
0 = Nil (no updates)
1 = All players
2 = Friends only
```

---

## 8. BanchoBot

The bot is a server-side entity — it exists as a `Player` object in the session but has no real client connection. It appears as user ID 1 to all players.

### 8.1 How the bot works

- **User ID:** Always 1 (hardcoded — the bot account must exist in the `users` table with `id = 1`).
- **No real connection:** The bot's `enqueue()` is a no-op — it never receives packets because there's no osu! client behind it.
- **Auto-friended:** Every player automatically has user ID 1 in their friends list (added during `relationships_from_sql()`).
- **Cached presence/stats:** Because every player has the bot as a friend, the client constantly requests its presence and stats. These are pre-built and cached to avoid rebuilding them thousands of times.
  - `bot_presence()` — fixed packet with fake coordinates (lat/lon way off the map), satellite provider country code (245), and a static privilege/mode byte.
  - `bot_stats()` — packet with a randomly chosen status from a preset list. The cache is cleared every 5 minutes to rotate the status text.
- **Bot statuses** (randomly rotated):
  - `(3, "the source code..")` — Editing
  - `(6, "geohot livestreams..")` — Watching
  - `(6, "asottile tutorials..")` — Watching
  - `(6, "over the server..")` — Watching
  - `(8, "out new features..")` — Testing
  - `(9, "a pull request..")` — Submitting

### 8.2 How the bot receives messages

The bot intercepts chat at two points in the bancho packet handlers:

**Public messages** (packet ID 1, `SendMessage` handler):
1. Message is sent to a channel.
2. Before broadcasting, the text is checked for the command prefix.
3. If it starts with the prefix → `process_commands()` is called.
4. The bot's response is sent to the channel via `channel.send_bot()`.

**Private messages** (packet ID 25, `SendPrivateMessage` handler):
1. When `recipient` resolves to the bot (user ID 1):
   - If message starts with command prefix → `process_commands()` runs, response goes back to sender.
   - If no command prefix → checks for `/np` (now playing) pattern. If found, calculates PP for the beatmap at multiple accuracy values (95%, 98%, 99%, 100%) and replies with the results.
2. Bot cannot be friended, unfriended, blocked, or match-invited — these handlers silently return when the target is the bot.

### 8.3 Command system

**Trigger:** `!` prefix (configurable). Messages not starting with `!` are normal chat.

**Dispatch flow:**
1. Strip `!` prefix from the message.
2. Split into `trigger` (first word) and `args` (remaining words).
3. First, check if `trigger` matches a sub-command set name (`mp`, `pool`, `clan`). If yes, re-parse: the next word becomes the sub-command trigger, and remaining words become args for the sub-command.
4. If no sub-command set matches, check `regular_commands`.
5. Find the command by trigger (or its aliases). If the player's privileges don't satisfy the command's required privileges → silently ignored.
6. Call the command callback with a `Context(player, trigger, args, recipient)`.
7. If the command returns a string, it becomes the bot's response.
8. **Hidden commands** (`hidden=True`): the bot's response goes only to staff and the command author, not the whole channel.

**Privilege check:** `player.priv & command.priv == command.priv` — the player must have ALL bits the command requires.

### 8.4 Regular commands (`!<command>`)

Available in any channel and DMs.

| Trigger(s) | Privilege | Description |
|------------|-----------|-------------|
| `help`, `h`, (blank) | UNRESTRICTED | Show all documented commands the player can access. Hidden. |
| `roll` | UNRESTRICTED | Roll an n-sided die (default 100). |
| `block` | UNRESTRICTED | Block a user from communicating with you. Hidden. Cannot block the bot. |
| `unblock` | UNRESTRICTED | Unblock a user. Hidden. |
| `reconnect` | UNRESTRICTED* | Disconnect and reconnect self (or another player if admin). |
| `changename` | SUPPORTER | Change your username. |
| `maplink`, `bloodcat`, `beatconnect`, `chimu`, `q` | UNRESTRICTED | Return a download link to your current beatmap. |
| `recent`, `last`, `r` | UNRESTRICTED | Show your most recent score. |
| `top` | UNRESTRICTED | Show your top 10 scores. Hidden. |
| `with`, `w` | UNRESTRICTED | Calculate PP with custom accuracy/mods using last `/np`. DM only. Hidden. |
| `request`, `req` | UNRESTRICTED | Request a beatmap for nomination. |
| `apikey` | UNRESTRICTED | Generate a new API key. DM only. |
| `requests`, `reqs` | NOMINATOR | Check nomination request queue. Hidden. |
| `map` | NOMINATOR | Change ranked status of last `/np` map. |
| `notes` | MODERATOR | View player logs. Hidden. |
| `addnote` | MODERATOR | Add a note to a player. Hidden. |
| `silence` | MODERATOR | Silence a player (time + unit). Hidden. |
| `unsilence` | MODERATOR | Remove a player's silence. Hidden. |
| `whitelist` | MODERATOR | Whitelist a player (bypass anticheat). Hidden. |
| `unwhitelist` | MODERATOR | Remove whitelist. Hidden. |
| `user`, `u` | ADMINISTRATOR | Show detailed user info. Hidden. |
| `restrict` | ADMINISTRATOR | Restrict a player. Hidden. |
| `unrestrict` | ADMINISTRATOR | Unrestrict a player. Hidden. |
| `alert` | ADMINISTRATOR | Send notification to all online players. Hidden. |
| `alertuser`, `alertu` | ADMINISTRATOR | Send notification to a specific player. Hidden. |
| `switchserv` | ADMINISTRATOR | Switch internal server endpoints. Hidden. |
| `shutdown` | ADMINISTRATOR | Gracefully shut down the server. |
| `stealth` | DEVELOPER | Toggle stealth mode (hidden from other players). |
| `recalc` | DEVELOPER | Recalculate PP for a player/map. |
| `debug` | DEVELOPER | Toggle debug mode. Hidden. |
| `addpriv` | DEVELOPER | Grant privileges to a player. Hidden. |
| `rmpriv` | DEVELOPER | Remove privileges from a player. Hidden. |
| `givedonator` | DEVELOPER | Give donator status for a specified duration. Hidden. |
| `wipemap` | DEVELOPER | Delete all scores for a beatmap. |
| `reload`, `re` | DEVELOPER | Hot-reload a Python module (development). |
| `server` | UNRESTRICTED | Show server performance stats. |
| `py` | DEVELOPER | Inline Python REPL (only in developer mode). |

### 8.5 Multiplayer commands (`!mp <subcommand>`)

Only work in a multiplayer match channel (`#multi_{id}`). The player must be in the match, and most commands require the player to be a referee (ref). The host is automatically a ref.

| Trigger(s) | Description |
|------------|-------------|
| `help`, `h` | Show mp commands. |
| `start`, `st` | Start the match. `st force` to force start immediately, `st cancel` to cancel timer, `st 30` to set 30s timer. |
| `abort`, `a` | Abort in-progress match. |
| `map` | Set the match's beatmap by ID. |
| `mods` | Set match mods from string (e.g. `mods hdhr`, `mods rx`). |
| `freemods`, `fm`, `fmods` | Toggle freemods on/off. |
| `host` | Transfer host to another player by name. |
| `randpw` | Randomize the match password. |
| `invite`, `inv` | Invite a player to the match by name. Cannot invite bot. |
| `addref` | Add a referee by name. |
| `rmref` | Remove a referee by name. |
| `listref` | List all referees. |
| `lock` | Lock all unused (empty) slots. |
| `unlock` | Unlock all slots. |
| `teams` | Change team type: `ffa`, `tag`, `teams`, `tag-teams`. |
| `condition`, `cond` | Change win condition: `score`, `acc`, `combo`, `scorev2`. |
| `scrim`, `autoref` | Start a scrim with best-of format (e.g. `scrim bo7`). |
| `endscrim`, `end` | End the current scrim. |
| `rematch`, `rm` | Restart scrim or roll back last match point. |
| `force`, `f` | ADMIN only. Force a player into the match by name. Hidden. |
| `loadpool`, `lp` | Load a mappool into the match by pool ID. |
| `unloadpool`, `ulp` | Unload the current mappool. |
| `ban` | Ban a mappool pick (e.g. `ban HD1`). |
| `unban` | Unban a mappool pick. |
| `pick` | Pick a map from the loaded pool. |

### 8.6 Mappool commands (`!pool <subcommand>`)

Requires `TOURNEY_MANAGER` privilege. All hidden.

| Trigger(s) | Description |
|------------|-------------|
| `help`, `h` | Show pool commands. |
| `create`, `c` | Create a new mappool with a name. |
| `delete`, `del`, `d` | Delete a mappool by ID. |
| `add`, `a` | Add your last `/np` map to a pool. Format: `pool add <pool_id> <mods><slot>` (e.g. `HD2`). |
| `remove`, `rm`, `r` | Remove a map from a pool. Format: `pool remove <pool_id> <mods><slot>`. |
| `list`, `l` | List all mappools. |
| `info`, `i` | Show a pool's details and all maps. |

### 8.7 Clan commands (`!clan <subcommand>`)

Available in any channel/DM.

| Trigger(s) | Description |
|------------|-------------|
| `help`, `h` | Show clan commands. |
| `create`, `c` | Create a clan. Format: `clan create <tag> <name>`. Sets creator as owner. Announces to `#announce`. |
| `disband`, `delete`, `d` | Disband your clan. Staff can disband others by tag. |
| `info`, `i` | Look up a clan by tag. Shows owner, members sorted by rank. |
| `leave` | Leave your current clan. |
| `list`, `l` | List all clans. |

### 8.8 `/np` (now playing) integration

The `/np` command is not a regular command — it's detected by a regex pattern match on chat messages (both public and DM). When a player types something like:
```
now playing: https://osu.ppy.sh/b/1234567
```
The parser extracts the beatmap ID, game mode, and mods, and stores them on `player.last_np`. Several commands then use this data:
- `!with` / `!w` — calculate PP for the beatmap at custom accuracy/mods
- `!map` — change the map's ranked status
- `!request` / `!req` — request the map for nomination
- `!pool add` — add the map to a mappool

When sent as a DM to the bot (without `!`), the bot auto-calculates PP at 95%, 98%, 99%, and 100% accuracy and replies with the results.

### 8.9 Welcome & notification messages

**On first login** (player not yet `VERIFIED`):
- Bot DMs a welcome message: server name, `!help` hint, Discord link.
- If user ID 3 (first registered user) — grants full staff privileges (STAFF, NOMINATOR, WHITELISTED, TOURNEY_MANAGER, DONATOR, ALUMNI).

**On every login** (all players):
- Popup notification: `"Welcome back to {SERVER_NAME}! Running bancho.py v{VERSION}-ex."`

**On restricted player login:**
- Bot DMs: `"Your account is currently in restricted mode. If you believe this is a mistake, or have waited a period greater than 3 months, you may appeal via the form on the site."`

### 8.10 Privileges reference

`IntFlag` bitmask used for both player privileges and command access control:

| Bit | Name | Value | Purpose |
|-----|------|-------|---------|
| 0 | `UNRESTRICTED` | `1` | Not restricted/banned |
| 1 | `VERIFIED` | `2` | First login completed |
| 2 | `WHITELISTED` | `4` | Bypasses anticheat checks |
| 4 | `SUPPORTER` | `16` | osu!supporter perks |
| 5 | `PREMIUM` | `32` | Premium donator |
| 7 | `ALUMNI` | `128` | Former staff |
| 10 | `TOURNEY_MANAGER` | `1024` | Tournament management, pool commands |
| 11 | `NOMINATOR` | `2048` | Beatmap nomination |
| 12 | `MODERATOR` | `4096` | Moderation tools (silence, notes, whitelist) |
| 13 | `ADMINISTRATOR` | `8192` | Server administration (restrict, alert, shutdown) |
| 14 | `DEVELOPER` | `16384` | Developer tools (reload, addpriv, rmp, py) |

Combined flags:
- `DONATOR = SUPPORTER | PREMIUM` (48)
- `STAFF = MODERATOR | ADMINISTRATOR | DEVELOPER` (28672)

### 8.11 Bot anti-abuse protections

- Bot cannot be blocked (`!block` returns error when targeting bot).
- Bot cannot be unfriended (`FRIEND_REMOVE` handler returns immediately).
- Bot cannot be friended manually (`FRIEND_ADD` handler returns immediately) — it's auto-added.
- Bot cannot be invited to matches (`!mp invite` and `MATCH_INVITE` packet both return "I'm too busy!").
- Silenced players cannot use any commands (message never reaches command processor).
- Commands a player lacks privileges for are silently ignored (no error response — as if the command doesn't exist).

### 8.12 Chat routing for bot responses

**Public channel command:**
1. Original `!command` message is broadcast to the channel normally (if not hidden).
2. Bot's response is sent to the channel via `channel.send_bot()`.
3. If hidden: original message goes only to staff, response to staff + author.

**DM command:**
1. Command is processed by `process_commands()`.
2. Response is delivered directly to the sender as a `SEND_MESSAGE` packet from the bot.
3. If no command matches AND the text contains `/np` → auto PP calculation response.
4. Offline: the message is not stored as mail (bot doesn't receive mail).

### 8.13 IRC integration

When `ENABLE_IRC` is on, the bot also responds to IRC clients. The command processor is the same — `process_commands()` is called from the IRC `handler_privmsg`. IRC-specific extras:
- `!mp make` — joins the player's multiplayer chat channel via IRC.
- `!mp close` — leaves the multiplayer channel and match.
