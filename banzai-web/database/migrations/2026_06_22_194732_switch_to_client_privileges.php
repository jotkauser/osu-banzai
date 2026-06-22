<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;

return new class extends Migration {
    public function up(): void
    {
        DB::statement("UPDATE users SET privileges = 5");
        DB::statement("ALTER TABLE users ALTER COLUMN privileges SET DEFAULT 5");
    }

    public function down(): void
    {
        DB::statement("UPDATE users SET privileges = 1");
        DB::statement("ALTER TABLE users ALTER COLUMN privileges SET DEFAULT 1");
    }
};
