#pragma once
#include <stdint.h>

// User-controlled display brightness, persisted to NVS. The middle (PWR)
// button short-press cycles through the levels via brightness_cycle().
// idle owns the actual panel brightness, so this routes the chosen level
// through idle_set_awake_brightness().
void    brightness_init(void);    // load saved level from NVS and apply
void    brightness_cycle(void);   // advance to next level, save, apply
uint8_t brightness_get(void);     // current PWM level (0..255)

// Percent-based control for the daemon path. pct is 0..100; the daemon-side
// payload may include a top-level "brightness" integer (0..100) which the
// firmware applies via this entry point. Persists to NVS under "brt_pwm".
void    brightness_set_pct(uint8_t pct);   // 0..100
uint8_t brightness_get_pct(void);          // current %, 0..100

