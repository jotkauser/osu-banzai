<?php

namespace App\Http\Controllers\OsuStable;

use App\Http\Controllers\Controller;
use App\Services\RegistrationService;
use Illuminate\Http\Request;

class InGameRegisterController extends Controller
{
    public function __construct(
        private RegistrationService $registration
    ) {}

    public function store(Request $request)
    {
        $username = $request->input('user.username');
        $email = $request->input('user.user_email');
        $password = $request->input('user.password');
        $check = (int) $request->input('check');

        if (!$username || !$email || !$password) {
            return response('Missing required params', 400);
        }

        if ($check === 1) {
            $errors = $this->registration->checkAvailability($username, $email);

            if (empty($errors)) {
                return response('ok');
            }

            return response()->json([
                'form_error' => ['user' => $errors],
            ], 400);
        }

        if ($check === 0) {
            $ip = $request->header('X-Real-IP', $request->ip());
            $result = $this->registration->register($username, $email, $password, $ip);

            if (!empty($result['errors'])) {
                $userErrors = $result['errors']['user'] ?? $result['errors'];
                return response()->json([
                    'form_error' => ['user' => $userErrors],
                ], 400);
            }

            return response('ok');
        }

        return response('Invalid check value', 400);
    }
}
