# BitAnimator 
Version: 1.0.2 preview (build 2020.07.18)


Unity editor extension for music visualization and animation recording

Changes in version 1.0:
---
* Testing animations in Play and Edit Mode
* Parameter for property: Damping - creates a smooth fading animation
* Parameter for property: Smoothness - completely smooths the animation making it less sharp
* Parameter for property: Accumulate - the animation does not subside and constantly accumulates
* Parameter for property: Lerp repeats - loop the animation when it reaches its maximum (useful with Accumulate)
* Modifiers for property: TimeRange - limits the recording of the animation effect by time (has 4 values: start of rise, start, end, smooth end)
* Added ability to compress animation file (Advanced settings / Animation quality)
* Updated GUI for BitAnimator
* Added the ability to switch the interface mode from simple to expert
* GUI for Window / BitAnimator allows you to test frequencies and peaks in music in test mode
* Calculations on the GPU (now animations can be rewritten up to 100 times faster)
* Added new filters: Exp, Hann Poison, Dolph Chebyshev

In development:
---
* GUI Window / BitAnimator
* Demo scenes - usage examples and templates
* Presets
* Modifiers for property
* Beat detector
* Beat Per Minute Analysis (BPM)
----

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
