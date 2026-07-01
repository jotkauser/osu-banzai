<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('beatmapsets', function (Blueprint $table) {
            $table->unsignedBigInteger('id')->primary();
            $table->string('artist', 128);
            $table->string('title', 128);
            $table->string('creator', 32);
            $table->tinyInteger('status')->default(0)->index();
            $table->text('tags')->nullable();
            $table->boolean("local")->default(false); // beatmapset from local bss, not osu.direct
            $table->timestamp('last_updated')->useCurrent();
            $table->timestamp("submitted_at");
            $table->timestamp("approved_at")->nullable();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('beatmapsets');
    }
};
