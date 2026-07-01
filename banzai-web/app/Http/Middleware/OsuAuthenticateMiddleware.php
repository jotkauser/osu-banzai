<?php

namespace App\Http\Middleware;

use Closure;
use App\Models\User;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Hash;

class OsuAuthenticateMiddleware
{
    public function handle(Request $request, Closure $next)
    {
        $username = $request->query("us") ?? $request->query('u');
        $passwordMd5 = $request->query('ha') ?? $request->query('h');

        if (empty($username) || empty($passwordMd5)) {
            return response('', 401);
        }

        $user = User::where('name', $username)->first();

        if ($user === null || !Hash::check($passwordMd5, $user->password)) {
            return response('', 401);
        }

        return $next($request);
    }
}
