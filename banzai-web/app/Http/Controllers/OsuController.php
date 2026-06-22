<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;

class OsuController extends Controller
{
    public function banchoConnect(Request $request)
    {
        return response('');
    }

    public function seasonal()
    {
        return response()->json([]);
    }

    public function checkUpdates()
    {
        return response('');
    }

    public function checkTweets()
    {
        return response('');
    }
}
