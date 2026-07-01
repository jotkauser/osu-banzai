<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('beatmaps', function (Blueprint $table) {
            $table->unsignedBigInteger('id')->primary();
            $table->foreignId('set_id')->constrained('beatmapsets')->cascadeOnDelete();
            $table->char('md5', 32)->unique();
            $table->string('version', 128);
            $table->string('filename', 256);
            $table->unsignedInteger('total_length')->default(0);
            $table->unsignedInteger('max_combo')->default(0);
            $table->unsignedTinyInteger('mode')->default(0)->index();
            $table->float('bpm')->default(0);
            $table->float('cs')->default(0);
            $table->float('ar')->default(0);
            $table->float('od')->default(0);
            $table->float('hp')->default(0);
            $table->float('diff')->default(0);
            $table->unsignedInteger('plays')->default(0);
            $table->unsignedInteger('passes')->default(0);
            $table->boolean('frozen')->default(false);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('beatmaps');
    }
};
