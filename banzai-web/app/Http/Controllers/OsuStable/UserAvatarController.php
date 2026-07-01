<?php

namespace App\Http\Controllers\OsuStable;

use App\Http\Controllers\Controller;
use Illuminate\Http\Request;

class UserAvatarController extends Controller
{
    public function getAvatar(Request $request, $userId)
    {
        $path = public_path('images/avatar.png');
        return response()->file($path);
    }
}
