<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class UserStat extends Model
{
    protected $primaryKey = ['user_id', 'mode'];
    public $incrementing = false;
    public $timestamps = false;

    protected $fillable = [
        'user_id', 'mode', 'ranked_score', 'total_score',
        'pp', 'playcount', 'accuracy', 'max_combo',
    ];

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }
}
