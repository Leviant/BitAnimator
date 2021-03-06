# BitAnimator 
Version: 1.0 preview (build 2019.07.16)

расширение редактора Unity для визуализации музыки и записи в анимацию

Изменения в версии 1.0:
---
* Тестирование анимации в PlayMode и в EditMode
* Параметр для property: Damping - создает плавное затухание анимации 
* Параметр для property: Smoothness - полностью сглаживает анимацию делая её менее резкой
* Параметр для property: Accumulate - анимация не спадает и постоянно накапливается
* Параметр для property: Lerp repeats - зацикливает анимацию по достижении максимума (полезно с Accumulate)
* Модификаторы для property: TimeRange - ограничивает запись эффекта анимации по времени (имеет 4 значения: начало подъема, начало, конец, плавное окончание)
* Добавлена возможность сжатия файла анимации (Advanced settings/Animation quality)
* Обновленный GUI для BitAnimator
* Добавлена возможность переключать режим интерфейса с простого до экспертного
* GUI для Window/BitAnimator позволяет в тестовом режиме просматривать частоты и пики в музыке
* Вычисления на GPU (теперь анимации можно перезаписывать до 100 раз быстрее)
* Добавлены новые фильтры: Exp, Hann Poison, Dolph Chebyshev

В разработке:
---
* GUI Window/BitAnimator
* Demo сцены - примеры использования и шаблоны
* Пресеты
* Модификаторы для property
* Детектор битов
* Анализ ударов в минуту (BPM)
