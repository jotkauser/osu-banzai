<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;

class UserAvatarController extends Controller
{
    public function getAvatar(Request $request, $userId)
    {
        $path = public_path('images/avatar.png');
        return response()->file($path);
    }
}
