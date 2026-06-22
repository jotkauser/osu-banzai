<?php

use App\Http\Controllers\Auth\LoginController;
use App\Http\Controllers\Auth\RegisterController;
use App\Http\Controllers\OsuController;
use App\Http\Controllers\OsuDirectController;
use App\Http\Controllers\PpyProxyController;
use App\Http\Controllers\UserAvatarController;
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

Route::domain("osu.{$host}")->group(function () use ($appUrl) {
    Route::redirect("/", "{$appUrl}");
    Route::get('/web/bancho_connect.php', [OsuController::class, 'banchoConnect']);
    Route::get('/web/osu-getseasonal.php', [OsuController::class, 'seasonal']);
    Route::get('/web/check-updates.php', [OsuController::class, 'checkUpdates']);
    Route::get('/web/osu-checktweets.php', [OsuController::class, 'checkTweets']);
    Route::get('/web/osu-search.php', [OsuDirectController::class, 'search'])->middleware('osu.auth');
    Route::get('/web/osu-search-set.php', [OsuDirectController::class, 'searchSet'])->middleware('osu.auth');
    Route::get('/d/{setId}', [OsuDirectController::class, 'download'])->where('setId', '\d+n?');
});

Route::domain("b.{$host}")->group(function () {
    Route::get("/thumb/{name}", [PpyProxyController::class, 'proxyThumbnail']);
    Route::get("/preview/{name}", [PpyProxyController::class, 'proxySongPreview']);
});

Route::domain("a.{$host}")->group(function () {
    Route::get("/{userId}", [UserAvatarController::class, 'getAvatar']);
});
