<?php

namespace App\Models;

use App\Enums\RankedStatus;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

class Beatmapset extends Model
{
    public $incrementing = false;
    public $timestamps = false;

    protected $primaryKey = 'id';
    protected $keyType = 'int';

    protected $fillable = [
        'id', 'artist', 'title', 'creator', 'status', 'tags',
        'local', 'last_updated', 'submitted_at', 'approved_at',
    ];

    protected function casts(): array
    {
        return [
            'local' => 'boolean',
            'last_updated' => 'datetime',
            'submitted_at' => 'datetime',
            'approved_at' => 'datetime',
            'status' => RankedStatus::class
        ];
    }

    public function beatmaps(): HasMany
    {
        return $this->hasMany(Beatmap::class, 'set_id');
    }
}
