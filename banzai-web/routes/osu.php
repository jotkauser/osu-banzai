<?php


use App\Http\Controllers\MapLeaderboardController;
use App\Http\Controllers\OsuController;
use App\Http\Controllers\OsuDirectController;
use App\Http\Controllers\ScoreSubmissionController;
use Illuminate\Support\Facades\Route;

$appUrl = config('app.url');
$host = parse_url($appUrl, PHP_URL_HOST);

Route::domain("osu.$host")->group(function () use ($appUrl) {
    Route::redirect("/", "$appUrl");
    Route::get('/bancho_connect.php', [OsuController::class, 'banchoConnect']);
    Route::get('/osu-getseasonal.php', [OsuController::class, 'seasonal']);
    Route::get('/check-updates.php', [OsuController::class, 'checkUpdates']);
    Route::get('/osu-checktweets.php', [OsuController::class, 'checkTweets']);
    Route::get("/maps/{filename}", [MapLeaderboardController::class, 'mapFile'])->where('filename', '.*');

    Route::middleware(['osu.auth'])->group(function () {
        Route::get('/osu-search.php', [OsuDirectController::class, 'search']);
        Route::get('/osu-search-set.php', [OsuDirectController::class, 'searchSet']);
        Route::get('/d/{setId}', [OsuDirectController::class, 'download'])->where('setId', '\d+n?');

        Route::get('/osu-osz2-getscores.php', [MapLeaderboardController::class, 'scores']);
        Route::post('/osu-getbeatmapinfo.php', [MapLeaderboardController::class, 'info']);
        Route::get('/osu-getreplay.php', [MapLeaderboardController::class, 'replay']);

        Route::post('/osu-submit-modular-selector.php', [ScoreSubmissionController::class, 'submit']);
        Route::post('/osu-submit-modular.php', [ScoreSubmissionController::class, 'submitLegacy']);

        Route::get('/lastfm.php', function() {
            return response('', 200);
        });
        Route::get("/osu-getfriends.php", [OsuController::class, 'getFriends']);
    });
});
