<?php

use App\Http\Controllers\Auth\LoginController;
use App\Http\Controllers\Auth\RegisterController;
use App\Http\Controllers\OsuController;
use Illuminate\Support\Facades\Route;

$host = parse_url(config('app.url'), PHP_URL_HOST);

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

Route::domain("osu.{$host}")->group(function () {
    Route::get('/web/bancho_connect.php', [OsuController::class, 'banchoConnect']);
    Route::get('/web/osu-getseasonal.php', [OsuController::class, 'seasonal']);
    Route::get('/web/check-updates.php', [OsuController::class, 'checkUpdates']);
    Route::get('/web/osu-checktweets.php', [OsuController::class, 'checkTweets']);
});
