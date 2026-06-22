<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        DB::table("chat_messages")->delete();
        Schema::table("chat_messages", function (Blueprint $table) {
            $table->dropColumn("to");
            $table->foreignId("channel_id")->nullable()->constrained("chat_channels")->cascadeOnDelete();
        });
    }

    public function down(): void
    {
        DB::table("chat_messages")->delete();
        Schema::table("chat_messages", function (Blueprint $table) {
            $table->dropConstrainedForeignId("channel_id");
            $table->string("to");
        });
    }
};
