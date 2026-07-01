<?php

namespace App\Services;

use App\Models\Beatmap;
use App\Models\Beatmapset;
use Exception;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Storage;

    class BeatmapService
    {
        private const string API_BASE = 'https://osu.direct';
        private const string REGEX = '/^(.+?) - (.+?)\s?\(([^)]+)\) \[([^]]+)]\.osu$/';
        public function resolveByOsuFile(string $osuFile): ?Beatmap {
            $beatmap = Beatmap::where('filename', $osuFile)->first();
            if ($beatmap) return $beatmap;
            if (!preg_match(self::REGEX, $osuFile, $matches)) return null;
            $artist = $matches[1];
            $title = $matches[2];
            $creator = $matches[3];
            // lookup online
            $beatmapSearch = Http::get(self::API_BASE . "/api/search", [
                "q" => "[creator=\"$creator\"] $artist $title",
            ]);
            $beatmapSearch = $beatmapSearch->json();
            if (empty($beatmapSearch)) return null;
            $matched = array_filter($beatmapSearch, function ($beatmap) use ($creator) {
                return isset($beatmap['Creator']) && $beatmap['Creator'] === $creator;
            });
            if (empty($matched)) return null;
            $matchedSet = reset($matched);

            $beatmapset = Beatmapset::find($matchedSet['SetID']);
            if (!$beatmapset) {
                $beatmapset = $this->mapBeatmapSet($matchedSet);
                $beatmapset->save();
            }
            foreach ($matchedSet['ChildrenBeatmaps'] as $mapData) {
                $expectedFilename = $this->createOsuFileName(
                    $matchedSet['Artist'],
                    $matchedSet['Title'],
                    $matchedSet['Creator'],
                    $mapData['DiffName']
                );

                if ($expectedFilename === $osuFile) {
                    $beatmap = Beatmap::find($mapData['BeatmapID']);
                    if (!$beatmap) {
                        $beatmap = $this->mapBeatmap($mapData, $beatmapset);
                        $beatmap->filename = $osuFile;
                        $beatmap->save();
                    }
                    return $beatmap;
                }
            }

            return null;
        }

        public function resolveByMd5(string $md5): ?Beatmap
        {
            $beatmap = Beatmap::where('md5', $md5)->first();
            if ($beatmap) return $beatmap;

            $response = Http::get(self::API_BASE . "/api/md5/$md5/set");

            if ($response->failed()) return null;

            $matchedSet = $response->json();
            if (empty($matchedSet) || !isset($matchedSet['SetID'])) return null;

            $beatmapset = Beatmapset::find($matchedSet['SetID']);
            if (!$beatmapset) {
                $beatmapset = $this->mapBeatmapSet($matchedSet);
                $beatmapset->save();
            }
            $targetBeatmap = null;
            foreach ($matchedSet['ChildrenBeatmaps'] as $mapData) {
                $currentMap = Beatmap::find($mapData['BeatmapID']);

                if (!$currentMap) {
                    $currentMap = $this->mapBeatmap($mapData, $beatmapset);
                    $currentMap->md5 = $mapData['FileMD5'] ?? $mapData['md5'] ?? null;

                    $currentMap->save();
                }
                if (($mapData['FileMD5'] ?? $mapData['md5'] ?? '') === $md5) {
                    $targetBeatmap = $currentMap;
                }
            }

            return $targetBeatmap;
        }

        public function ensureOsuFile(Beatmap $beatmap): string {
            $osuFilePath = "beatmaps/$beatmap->filename";
            if (!Storage::exists($osuFilePath)) {
                $response = Http::get(self::API_BASE . "/api/osu/$beatmap->id");
                if ($response->failed()) {
                    throw new Exception("Failed to download osu file for beatmap ID $beatmap->id");
                }
                Storage::put($osuFilePath, $response->body());
            }
            return Storage::get($osuFilePath);
        }

        private function mapBeatmapSet($mapset): Beatmapset {
            return new Beatmapset([
                'id' => $mapset['SetID'],
                'artist' => $mapset['Artist'],
                'title' => $mapset['Title'],
                'creator' => $mapset['Creator'],
                'status' => $mapset['RankedStatus'],
                'local' => false,
                'tags' => $mapset['Tags'],
                'submitted_at' => $mapset['SubmittedDate'],
                'approved_at' => $mapset['ApprovedDate'],
            ]);
        }

        private function createOsuFileName(string $artist, string $title, string $creator, string $version): string {
            return sprintf(
                "%s - %s (%s) [%s].osu",
                $artist,
                $title,
                $creator,
                $version
            );
        }
        private function mapBeatmap($map, Beatmapset $set): Beatmap {
            return new Beatmap([
                "id" => $map["BeatmapID"],
                "set_id" => $map["ParentSetID"],
                "md5" => $map["FileMD5"],
                "version" => $map["DiffName"],
                "filename" => $this->createOsuFileName(
                    $set->artist,
                    $set->title,
                    $set->creator,
                    $map["DiffName"]
                ),
                "total_length" => $map["TotalLength"],
                "max_combo" => $map["MaxCombo"],
                "mode" => $map["Mode"],
                "bpm" => $map["BPM"],
                "cs" => $map["CS"],
                "ar" => $map["AR"],
                "od" => $map["OD"],
                "hp" => $map["HP"],
                "diff" => $map["DifficultyRating"],
                "plays" => 0,
                "passes" => 0,
                "frozen" => $set->status->isFrozen()
            ]);
        }
    }
