<?php

namespace App\Http\Controllers;

use App\Models\User;
use App\Models\UserFriend;
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

    public function getFriends(Request $request)
    {
        $user = User::where('name', $request->query('u'))->first();
        if (!$user) return response('', 401);

        $friendIds = UserFriend::where('user_id', $user->id)->pluck('friend_id');

        return response($friendIds->implode("\n"));
    }
}
