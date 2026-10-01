using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Water
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public class SonicWaterPlayer : MonoBehaviour
    {
        public float AirRemaining { get; private set; } = 30;
        public bool Underwater { get; private set; }
        PlayerBhysics player;
        Action01_Jump jump;
        HurtControl hurt;
        Rigidbody body;
        SonicWaterVolume water, breathingWater;
        float topSpeed, maxSpeed, acceleration, fallSpeed, jumpSpeed;
        Vector3 gravity, previousPosition;
        bool modified, previousValid, wasDead, alarmPaused, alarmStarted;
        float breathCapacity = 30;
        AudioSource alarm;
        readonly Dictionary<AudioSource, bool> muted = new Dictionary<AudioSource, bool>();
        GUIStyle numberStyle, labelStyle;

        void Awake()
        {
            player = GetComponent<PlayerBhysics>(); body = GetComponent<Rigidbody>();
            jump = GetComponent<Action01_Jump>(); hurt = GetComponent<HurtControl>();
            alarm = gameObject.AddComponent<AudioSource>();
            alarm.playOnAwake = false; alarm.loop = false; alarm.spatialBlend = 0;
        }
        void FixedUpdate()
        {
            if (player == null || body == null || Time.timeScale == 0) return;
            bool dead = hurt != null && hurt.isDead;
            if (dead || !player.enabled || body.isKinematic)
            {
                ResetWater(); wasDead = dead; return;
            }
            if (wasDead) { previousValid = false; wasDead = false; }
            Vector3 position = body.position;
            SonicWaterVolume next = null, head = null;
            bool splashed = false;
            foreach (var volume in SonicWaterVolume.Active)
            {
                if (volume == null || !volume.isActiveAndEnabled) continue;
                if (volume.Contains(position + Vector3.up * 0.2f)) next = volume;
                if (volume.Contains(position + Vector3.up * volume.breathingHeight)) head = volume;
                if (!splashed && previousValid && water == null && volume.CrossesSurface(previousPosition, position, out Vector3 hit))
                {
                    float drop = Mathf.Max(0, -Vector3.Dot(body.linearVelocity, volume.transform.up));
                    volume.Splash(hit, drop); splashed = true;
                }
            }
            if (next != water)
            {
                RestorePhysics();
                if (next != null)
                {
                    if (water == null && !splashed)
                    {
                        Vector3 local = next.transform.InverseTransformPoint(position); local.y = 0;
                        next.Splash(next.transform.TransformPoint(local), 0);
                    }
                    ApplyPhysics(next);
                }
                water = next;
            }
            if (next == null && modified) RestorePhysics();
            if (water != null)
            {
                Vector3 velocity = body.linearVelocity;
                float damping = Mathf.Exp(-water.horizontalDrag * Time.fixedDeltaTime);
                velocity.x *= damping; velocity.z *= damping;
                velocity.y = Mathf.Max(velocity.y, -water.maximumFallSpeed);
                body.linearVelocity = velocity;
            }
            Underwater = head != null;
            if (head == null)
            {
                AirRemaining = breathCapacity; breathingWater = null; StopAlarm();
            }
            else
            {
                if (breathingWater == null)
                {
                    breathCapacity = head.airSeconds; AirRemaining = breathCapacity;
                }
                breathingWater = head;
                AirRemaining = Mathf.Max(0, AirRemaining - Time.fixedDeltaTime);
                if (AirRemaining <= head.warningSeconds && !alarmStarted) StartAlarm(head);
                if (AirRemaining <= 0) Drown();
            }
            previousPosition = position; previousValid = true;
        }
        void ApplyPhysics(SonicWaterVolume volume)
        {
            topSpeed = player.TopSpeed; maxSpeed = player.MaxSpeed; acceleration = player.MoveAccell;
            fallSpeed = player.MaxFallingSpeed; gravity = player.Gravity;
            if (jump != null) jumpSpeed = jump.JumpSpeed;
            modified = true;
            player.TopSpeed = topSpeed * volume.speedMultiplier;
            player.MaxSpeed = maxSpeed * volume.speedMultiplier;
            player.MoveAccell = acceleration * volume.speedMultiplier;
            player.Gravity = gravity * volume.gravityMultiplier;
            player.MaxFallingSpeed = -volume.maximumFallSpeed;
            if (jump != null) jump.JumpSpeed = jumpSpeed * volume.jumpMultiplier;
        }
        void RestorePhysics()
        {
            if (!modified || player == null) return;
            player.TopSpeed = topSpeed; player.MaxSpeed = maxSpeed; player.MoveAccell = acceleration;
            player.Gravity = gravity; player.MaxFallingSpeed = fallSpeed;
            if (jump != null) jump.JumpSpeed = jumpSpeed;
            modified = false;
        }
        void Drown()
        {
            if (hurt == null)
            {
                Debug.LogError("Noyade : HurtControl manque sur Sonic.", this);
                ResetWater(); enabled = false; return;
            }
            if (hurt.isDead) return;
            hurt.isDead = true;
            var sounds = GetComponent<Objects_Interaction>();
            if (sounds != null && sounds.Sounds != null) sounds.Sounds.DieSound();
            body.linearVelocity = Vector3.zero;
            ResetWater();
        }
        void StartAlarm(SonicWaterVolume volume)
        {
            alarmStarted = true;
            var options = Object.FindAnyObjectByType<LoadSound>();
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>())
            {
                if (source == alarm || !source.isPlaying) continue;
                bool music = source == volume.levelMusic || (options != null && options.Music != null &&
                    source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.audioMixer == options.Music);
                if (!music) continue;
                muted[source] = source.mute; source.mute = true;
                if (alarm.outputAudioMixerGroup == null) alarm.outputAudioMixerGroup = source.outputAudioMixerGroup;
            }
            alarm.clip = volume.drowningMusic; alarm.volume = volume.warningVolume;
            if (alarm.clip != null)
            {
                alarm.time = Mathf.Clamp(volume.musicStartTime, 0, Mathf.Max(0, alarm.clip.length - 0.01f));
                alarm.Play();
            }
        }
        void StopAlarm()
        {
            if (alarm != null) { alarm.Stop(); alarm.clip = null; alarmPaused = false; }
            foreach (var pair in muted) if (pair.Key != null) pair.Key.mute = pair.Value;
            muted.Clear();
            alarmStarted = false;
        }
        void Update()
        {
            if (alarm == null || alarm.clip == null) return;
            if (Time.timeScale == 0 && !alarmPaused) { alarm.Pause(); alarmPaused = true; }
            else if (Time.timeScale > 0 && alarmPaused) { alarm.UnPause(); alarmPaused = false; }
        }
        void ResetWater()
        {
            RestorePhysics(); StopAlarm(); water = null; breathingWater = null;
            AirRemaining = breathCapacity; Underwater = false; previousValid = false;
        }
        void OnDisable() { ResetWater(); }
        void OnDestroy()
        {
            if (alarm == null) return;
            if (Application.isPlaying) Destroy(alarm); else DestroyImmediate(alarm);
        }
        void OnGUI()
        {
            if (!Underwater || breathingWater == null || AirRemaining > breathingWater.warningSeconds || (hurt != null && hurt.isDead)) return;
            if (numberStyle == null)
            {
                numberStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                labelStyle = new GUIStyle(numberStyle);
            }
            float scale = Mathf.Clamp(Screen.height / 900f, 0.6f, 2);
            numberStyle.fontSize = Mathf.RoundToInt(64 * scale); labelStyle.fontSize = Mathf.RoundToInt(18 * scale);
            numberStyle.normal.textColor = AirRemaining <= 3 ? new Color(1,0.35f,0.25f) : Color.white;
            labelStyle.normal.textColor = Color.white;
            Rect box = new Rect(Screen.width * 0.5f - 68 * scale, 70 * scale, 136 * scale, 116 * scale);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x, box.y + 4 * scale, box.width, 28 * scale), "AIR", labelStyle);
            GUI.Label(new Rect(box.x, box.y + 28 * scale, box.width, 80 * scale), Mathf.CeilToInt(AirRemaining).ToString(), numberStyle);
        }
    }
}
