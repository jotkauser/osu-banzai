<?php

namespace App\Http\Controllers;

use App\Enums\OsuRuleset;
use App\Enums\StableLeaderboardType;
use App\Models\Beatmap;
use App\Services\BeatmapService;
use Illuminate\Http\Request;

class MapLeaderboardController extends Controller
{
    public function __construct(
        protected BeatmapService $beatmapService
    ) {}

    public function scores(Request $request)
    {
        $v = (int) $request->input('v', 0);
        $m = (int) $request->input('m', 0);

        $isFromEditor = $request->boolean('s');
        $leaderboardType = StableLeaderboardType::tryFrom($v) ?? StableLeaderboardType::Global;
        $mapMd5 = $request->input('c');
        $mapFilename = $request->input('f');
        $ruleset = OsuRuleset::tryFrom($m) ?? OsuRuleset::Standard;
        $setId = (int) $request->input('i');
        $mods = (int) $request->input('mods');
        $mapsetMd5 = $request->input('h');

        $beatmap = Beatmap::where('md5', $mapMd5)->first();
        if (!$beatmap) {
            $beatmap = $this->beatmapService->resolveByMd5($mapMd5);
            if (!$beatmap) {
                // user has out-of-date map? try to resolve with name
                $beatmap = $this->beatmapService->resolveByOsuFile($mapFilename);
                if (!$beatmap) {
                    // not submitted
                    return response('-1|false');
                } else {
                    // out of date client-side
                    return response('1|true');
                }
            }

        }
        // return empty response, todo: return correct scores (needs score submission)
        $beatmapId = $beatmap->id;
        $beatmapsetId = $beatmap->set_id;
        $beatmapRankedStatus = $beatmap->set->status->toOsuStable();
        $beatmapName = "{$beatmap->set->artist} - {$beatmap->set->title} [$beatmap->version]";

        return response(implode("\n", [
            "$beatmapRankedStatus|false|$beatmapId|$beatmapsetId|0|0|",
            '0',
            $beatmapName,
            '0.0',
            '',
            '',
        ]));
    }

    public function info(Request $request)
    {
        $lines = [];

        foreach ($request->input('Filenames', []) as $idx => $filename) {
            $beatmap = Beatmap::with('set')->where('filename', $filename)->first();
            if (!$beatmap) {
                $beatmapService = new BeatmapService();
                $beatmap = $beatmapService->resolveByOsuFile($filename);
            }

            $grades = implode('|', ['N', 'N', 'N', 'N']);

            $lines[] = implode('|', [
                $idx,
                $beatmap->id,
                $beatmap->set_id,
                $beatmap->md5,
                $beatmap->set->status->value,
                $grades,
            ]);
        }

        return response(implode("\n", $lines));
    }

    public function mapFile(string $filename)
    {
        $beatmap = $this->beatmapService->resolveByOsuFile($filename);
        if (!$beatmap) {
            return response()->noContent(404);
        }
        $osuFile = $this->beatmapService->ensureOsuFile($beatmap);
        return response($osuFile);
    }

    public function replay(Request $request) {}
}
