<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class ChatChannelSeeder extends Seeder
{
    public function run(): void
    {
        DB::table('chat_channels')->insert([
            ['name' => '#osu', 'description' => 'General osu! chat'],
            ['name' => '#banzai', 'description' => 'Stuff about banzai server'],
            ['name' => '#announce', 'description' => 'Announcements'],
        ]);
    }
}
