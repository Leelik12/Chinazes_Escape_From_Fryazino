using UnityEngine;

namespace RacingProject.Enemy
{
    // Машина игроков как цель врагов. Если её ещё нет (сцена грузится) или её выключили и включили заново,
    // враги ищут её снова, но по тегу не чаще раза в секунду на всех сразу
    public static class PlayerCarTarget
    {
        private const float SearchInterval = 1f;
        private static Transform cached;
        private static float nextSearch;

        // Годится ли цель: машина на месте и включена
        public static bool Valid(Transform target)
        {
            return target != null && target.gameObject.activeInHierarchy;
        }

        public static Transform Find()
        {
            if (Valid(cached)) return cached;
            if (Time.time < nextSearch) return null;
            nextSearch = Time.time + SearchInterval;
            GameObject car = GameObject.FindWithTag(Tags.Car);
            cached = car != null ? car.transform : null;
            return cached;
        }

        // Обновляет цель, если старая пропала; true — цель есть
        public static bool Refresh(ref Transform target)
        {
            if (Valid(target)) return true;
            target = Find();
            return target != null;
        }
    }
}
