<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class Beatmap extends Model
{
    public $incrementing = false;
    public $timestamps = false;

    protected $primaryKey = 'id';
    protected $keyType = 'int';

    protected $fillable = [
        'id', 'set_id', 'md5', 'version', 'filename',
        'total_length', 'max_combo', 'mode', 'bpm',
        'cs', 'ar', 'od', 'hp', 'diff',
        'plays', 'passes', 'frozen'
    ];

    protected function casts(): array
    {
        return [
            'frozen' => 'boolean'
        ];
    }

    public function set(): BelongsTo
    {
        return $this->belongsTo(Beatmapset::class, 'set_id');
    }
}
