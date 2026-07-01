<?php

use App\Http\Controllers\Auth\LoginController;
use App\Http\Controllers\Auth\RegisterController;
use App\Http\Controllers\OsuStable\InGameRegisterController;
use App\Http\Controllers\OsuStable\PpyProxyController;
use App\Http\Controllers\OsuStable\UserAvatarController;
use Illuminate\Support\Facades\Route;

$appUrl = config('app.url');
$host = parse_url($appUrl, PHP_URL_HOST);

Route::domain($host)->group(function () {
    Route::get('/', function () {
        return inertia('Home');
    })->name('home');

    Route::middleware('guest')->group(function () {
        Route::get('/login', [LoginController::class, 'create'])->name('login');
        Route::post('/login', [LoginController::class, 'store']);
        Route::get('/register', [RegisterController::class, 'create'])->name('register');
        Route::post('/register', [RegisterController::class, 'store']);
    });

    Route::post('/logout', [LoginController::class, 'destroy'])->middleware('auth')->name('logout');
});

Route::domain("osu.$host")->group(function () {
    Route::post("/users", [InGameRegisterController::class, 'store']);
});

Route::domain("b.$host")->group(function () {
    Route::get("/thumb/{name}", [PpyProxyController::class, 'proxyThumbnail']);
    Route::get("/preview/{name}", [PpyProxyController::class, 'proxySongPreview']);
});

Route::domain("a.$host")->group(function () {
    Route::get("/{userId}", [UserAvatarController::class, 'getAvatar']);
});
