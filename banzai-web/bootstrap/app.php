<?php

use App\Http\Middleware\HandleInertiaRequests;
use Dotenv\Dotenv;
use Illuminate\Foundation\Application;
use Illuminate\Foundation\Configuration\Exceptions;
use Illuminate\Foundation\Configuration\Middleware;
use Illuminate\Http\Request;
use App\Http\Middleware\OsuAuthenticateMiddleware;

$rootEnv = dirname(__DIR__) . '/../.env';

if (file_exists($rootEnv)) {
    Dotenv::createMutable(dirname($rootEnv))->load();
}

return Application::configure(basePath: dirname(__DIR__))
    ->withRouting(
        web: __DIR__.'/../routes/web.php',
        commands: __DIR__.'/../routes/console.php',
        health: '/up',
        then: function () {
            Route::middleware("api")
                ->prefix("web")
                ->group(base_path("routes/osu.php"));
        }
    )
    ->withMiddleware(function (Middleware $middleware): void {
        $middleware->web(append: [
            HandleInertiaRequests::class,
        ]);
        $middleware->preventRequestForgery(except: [
            'web/osu-submit-modular*',
            'web/osu-getbeatmapinfo.php',
        ]);
        $middleware->alias([
            'osu.auth' => OsuAuthenticateMiddleware::class,
        ]);
    })
    ->withExceptions(function (Exceptions $exceptions): void {
        $exceptions->shouldRenderJsonWhen(
            fn (Request $request) => $request->is('api/*'),
        );
    })->create();
