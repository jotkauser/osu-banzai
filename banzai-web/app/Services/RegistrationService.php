<?php

namespace App\Services;

use App\Models\User;
use Illuminate\Support\Facades\Hash;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Validator;

class RegistrationService
{
    public function checkAvailability(string $username, string $email): array
    {
        $errors = [];

        if (User::where('name', $username)->exists()) {
            $errors['username'] = ['Username already taken.'];
        }

        if (User::where('email', $email)->exists()) {
            $errors['user_email'] = ['Email already in use.'];
        }

        return $errors;
    }

    public function register(string $username, string $email, string $password, string $ip): array
    {
        $validator = Validator::make(
            ['username' => $username, 'email' => $email, 'password' => $password],
            [
                'username' => 'required|string|min:3|max:15|regex:/^[a-zA-Z0-9_\[\] -]+$/',
                'email'    => 'required|email|max:255',
                'password' => 'required|string|min:8|max:64',
            ],
        );

        if ($validator->fails()) {
            return ['errors' => $validator->errors()->toArray()];
        }

        $availabilityErrors = $this->checkAvailability($username, $email);
        if (!empty($availabilityErrors)) {
            return ['errors' => ['user' => $availabilityErrors]];
        }

        $country = $this->resolveCountry($ip);

        User::create([
            'name'     => $username,
            'email'    => $email,
            'password' => Hash::make(md5($password)),
            'country'  => $country,
        ]);

        return [];
    }

    private function resolveCountry(string $ip): string
    {
        if ($ip === '127.0.0.1' || $ip === '::1') {
            return 'XX';
        }

        $response = Http::get("http://ip-api.com/json/$ip?fields=countryCode");
        if ($response->successful()) {
            return $response->json('countryCode', 'XX');
        }

        return 'XX';
    }
}
