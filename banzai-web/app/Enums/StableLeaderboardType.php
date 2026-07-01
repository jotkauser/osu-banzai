<?php

namespace App\Enums;

enum StableLeaderboardType: int {
    case Local = 0;
    case Global = 1;
    case GlobalMods = 2;
    case Friends = 3;
    case Country = 4;
}
