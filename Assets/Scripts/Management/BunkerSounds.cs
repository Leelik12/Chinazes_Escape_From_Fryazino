using UnityEngine;

namespace RacingProject.Management
{
    // Звуки бункера меню: гул вентиляции, гудение ламп, эфир радиостанции с морзянкой, щелчки кнопок терминала
    // и тумблеров, далёкий разрыв, а для выхода из бункера — ревун тревоги, штурвал, лязг кремальер и открытие
    // тяжёлой двери. Клипы синтезирует Tools/Audio/generate_bunker_sounds.py в Assets/Resources/Bunker
    // (циклы склеены без щелчка), здесь они загружаются при первом обращении. Сторонних файлов и лицензий нет
    public static class BunkerSounds
    {
        private static AudioClip vent, buzz, radio, hover, click, toggle, rumble, klaxon, ratchet, creak;
        private static AudioClip[] clanks;

        public static AudioClip Vent => vent != null ? vent : vent = Load("BunkerVent");
        public static AudioClip LampBuzz => buzz != null ? buzz : buzz = Load("BunkerLampBuzz");
        public static AudioClip Radio => radio != null ? radio : radio = Load("BunkerRadio");
        public static AudioClip Hover => hover != null ? hover : hover = Load("TerminalHover");
        public static AudioClip Click => click != null ? click : click = Load("TerminalClick");
        public static AudioClip Toggle => toggle != null ? toggle : toggle = Load("ToggleSnap");
        public static AudioClip Rumble => rumble != null ? rumble : rumble = Load("DistantShelling");
        // Цикл 1 с: гудок 0,6 с и пауза, в которую ложится отзвук комнаты
        public static AudioClip Klaxon => klaxon != null ? klaxon : klaxon = Load("AlarmKlaxon");
        public static AudioClip Ratchet => ratchet != null ? ratchet : ratchet = Load("WheelRatchet");
        public static AudioClip Creak => creak != null ? creak : creak = Load("DoorCreak");

        // Три варианта лязга кремальеры, чтобы соседние удары не звучали одинаково
        public static AudioClip Clank(int index)
        {
            if (clanks == null)
                clanks = new[] { Load("DogClank_0"), Load("DogClank_1"), Load("DogClank_2") };
            return clanks[Mathf.Abs(index) % clanks.Length];
        }

        private static AudioClip Load(string name)
        {
            AudioClip clip = Resources.Load<AudioClip>("Bunker/" + name);
            if (clip == null) Debug.LogWarning("BunkerSounds: нет клипа Resources/Bunker/" + name);
            return clip;
        }
    }
}
