<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use Illuminate\Support\Facades\Http;

class PpyProxyController extends Controller
{
    public function proxyThumbnail(Request $request, $name)
    {
        $targetUrl = "https://b.ppy.sh/thumb/{$name}";
        $response = Http::withHeaders([
            'User-Agent' => 'osu!',
            'Accept' => 'image/avif,image/webp,image/apng,image/*,*/*;q=0.8',
        ])->get($targetUrl);

        if ($response->failed()) {
            return response($response->body(), $response->status());
        }

        $imageContent = $response->body();
        $contentType = $response->header('Content-Type') ?: 'image/jpeg';

        return response($imageContent, 200, ['Content-Type' => $contentType]);
    }

    public function proxySongPreview(Request $request, $name)
    {
        $targetUrl = "https://b.ppy.sh/preview/{$name}";
        $response = Http::withHeaders([
            'User-Agent' => 'osu!',
            'Accept' => 'audio/mp3,application/octet-stream,application/x-msdownload,*/*;q=0.8',
        ])->get($targetUrl);

        if ($response->failed()) {
            return response($response->body(), $response->status());
        }

        $audioContent = $response->body();
        $contentType = $response->header('Content-Type') ?: 'audio/mp3';

        return response($audioContent, 200, ['Content-Type' => $contentType]);
    }
}
