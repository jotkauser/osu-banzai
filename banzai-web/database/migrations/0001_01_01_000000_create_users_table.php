<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        Schema::create("users", function (Blueprint $table) {
            $table->id();
            $table->string("name");
            $table->string("email")->unique();
            $table->timestamp("email_verified_at")->nullable();
            $table->string("password");
            $table->char("country", 2)->default("XX");
            $table->integer("privileges")->default(1);
            $table->rememberToken();
            $table->timestamps();
        });

        Schema::create("user_stats", function (Blueprint $table) {
            $table->foreignId("user_id")->constrained("users")->cascadeOnDelete();
            $table->unsignedTinyInteger("mode")->default(0);
            $table->unsignedBigInteger("ranked_score")->default(0);
            $table->unsignedBigInteger("total_score")->default(0);
            $table->unsignedInteger("pp")->default(0);
            $table->unsignedInteger("playcount")->default(0);
            $table->float("accuracy")->default(0.0);
            $table->unsignedInteger("max_combo")->default(0);

            $table->primary(["user_id", "mode"]);
        });

        Schema::create("password_reset_tokens", function (Blueprint $table) {
            $table->string("email")->primary();
            $table->string("token");
            $table->timestamp("created_at")->nullable();
        });

        Schema::create("sessions", function (Blueprint $table) {
            $table->string("id")->primary();
            $table->foreignId("user_id")->nullable()->index();
            $table->string("ip_address", 45)->nullable();
            $table->text("user_agent")->nullable();
            $table->longText("payload");
            $table->integer("last_activity")->index();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists("user_stats");
        Schema::dropIfExists("users");
        Schema::dropIfExists("password_reset_tokens");
        Schema::dropIfExists("sessions");
    }
};
