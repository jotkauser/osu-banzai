<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use Illuminate\Support\Facades\Http;

class OsuDirectController extends Controller
{
    public function search(Request $request)
    {
        $params = [
            'amount' => 100,
            'offset' => (int) $request->query('p') * 100,
        ];

        $query = $request->query('q');
        if (!in_array($query, ['Newest', 'Top+Rated', 'Most+Played'])) {
            $params['query'] = $query;
        }

        $mode = (int) $request->query('m', '-1');
        if ($mode !== -1) {
            $params['mode'] = $mode;
        }

        $status = (int) $request->query('r', '4');
        if ($status !== 4) {
            $params['status'] = $this->rankedStatus($status);
        }

        $response = Http::get(env('MIRROR_SEARCH_ENDPOINT'), $params);
        if (!$response->successful()) {
            return response("-1\nFailed to retrieve data from the beatmap mirror.");
        }

        $result = $response->json();
        $count = count($result);

        $lines = [(string) ($count === 100 ? 101 : $count)];

        foreach ($result as $set) {
            if (empty($set['ChildrenBeatmaps'])) {
                continue;
            }

            $set['HasVideo'] = (int) $set['HasVideo'];

            usort($set['ChildrenBeatmaps'], fn($a, $b) =>
                $a['DifficultyRating'] <=> $b['DifficultyRating']
            );

            $diffs = implode(',', array_map(fn($row) => sprintf(
                '[%.2f⭐] %s {cs: %s / od: %s / ar: %s / hp: %s}@%s',
                $row['DifficultyRating'],
                str_replace('|', 'I', $row['DiffName']),
                $row['CS'], $row['OD'], $row['AR'], $row['HP'], $row['Mode'],
            ), $set['ChildrenBeatmaps']));

            $lines[] = sprintf(
                '%s.osz|%s|%s|%s|%s|10.0|%s|%s|0|%s|0|0|0|%s',
                $set['SetID'],
                str_replace('|', 'I', $set['Artist']),
                str_replace('|', 'I', $set['Title']),
                $set['Creator'],
                $set['RankedStatus'],
                $set['LastUpdate'],
                $set['SetID'],
                $set['HasVideo'],
                $diffs,
            );
        }

        return response(implode("\n", $lines));
    }

    public function searchSet(Request $request)
    {
        $params = [];
        if ($id = $request->query('s')) $params['s'] = (int) $id;
        if ($id = $request->query('b')) $params['b'] = (int) $id;
        if ($hash = $request->query('c')) $params['c'] = $hash;

        if (empty($params)) {
            return response('');
        }

        $response = Http::get(env('MIRROR_SEARCH_ENDPOINT'), $params);
        if (!$response->successful()) {
            return response('');
        }

        $sets = $response->json();
        if (empty($sets)) {
            return response('');
        }

        $set = $sets[0];
        if (empty($set['ChildrenBeatmaps'])) {
            return response('');
        }

        $lines = [];
        foreach ($set['ChildrenBeatmaps'] as $map) {
            $lines[] = sprintf(
                '%s.osz|%s|%s|%s|%s|10.0|%s|%s|0|%s|0|0|0|%s|%s|%s|%s|%s|%s|%s|%s|%s',
                $set['SetID'],
                str_replace('|', 'I', $set['Artist']),
                str_replace('|', 'I', $set['Title']),
                $set['Creator'],
                $set['RankedStatus'],
                $set['LastUpdate'],
                $set['SetID'],
                $set['HasVideo'] ?? 0,
                $map['DiffName'],
                $map['DifficultyRating'],
                $map['Mode'],
                $map['BPM'] ?? 0,
                $map['CS'],
                $map['AR'],
                $map['OD'],
                $map['HP'],
                $map['SR'] ?? 0,
                $map['Length'] ?? 0,
                $map['MaxCombo'] ?? 0,
                $map['Playcount'] ?? 0,
            );
        }

        return response(implode("\n", $lines));
    }

    public function download(string $setId)
    {
        $noVideo = str_ends_with($setId, 'n');
        if ($noVideo) $setId = substr($setId, 0, -1);

        $url = env('MIRROR_DOWNLOAD_ENDPOINT') . '/' . $setId . '?n=' . ($noVideo ? '1' : '0');

        $response = Http::get($url);
        return response($response->body(), 200, [
            'Content-Type' => 'application/octet-stream',
            'Content-Disposition' => "attachment; filename=\"{$setId}.osz\"",
        ]);
    }

    private function rankedStatus(int $directStatus): int
    {
        return match ($directStatus) {
            0 => 1,  // Ranked
            2 => 2,  // Approved
            3 => 3,  // Qualified
            4 => 4,  // Loved
            5 => 0,  // Pending
            default => $directStatus,
        };
    }
}
