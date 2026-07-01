<?php

namespace App\Enums;

enum RankedStatus: int
{
    case Graveyard = -2;
    case WIP = -1;
    case Pending = 0;
    case Ranked = 1;
    case Approved = 2;
    case Qualified = 3;
    case Loved = 4;

    public function hasLeaderboard(): bool
    {
        return in_array($this, [self::Ranked, self::Approved, self::Qualified, self::Loved]);
    }

    public function awardsPP(): bool
    {
        return in_array($this, [self::Ranked, self::Approved]);
    }

    public function isFrozen(): bool
    {
        return in_array($this, [self::Ranked, self::Approved]);
    }

    public function toOsuStable(): int
    {
        return match ($this) {
            self::Graveyard,
            self::WIP,
            self::Pending   => 0,
            self::Approved  => 3,
            self::Qualified => 4,
            self::Loved     => 5,
            default         => 2,
        };
    }
}
