<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('pending_direct_messages', function (Blueprint $table) {
            $table->id();
            $table->foreignId('from_id')->constrained('users')->cascadeOnDelete();
            $table->foreignId('to_id')->constrained('users')->cascadeOnDelete();
            $table->string('from_name');
            $table->string('message', 2048);
            $table->timestamp('created_at')->useCurrent();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('pending_direct_messages');
    }
};
