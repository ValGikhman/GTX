(function ($, window) {
    "use strict";

    var pluginName = "gtxRangeFilter";
    var dataKey = "gtx.rangeFilter";

    function parseNumber(value, fallback) {
        var parsed = parseFloat(String(value == null ? "" : value).replace(/[^0-9.\-]/g, ""));
        return isNaN(parsed) ? fallback : parsed;
    }

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function roundToStep(value, step) {
        if (!step || step <= 0) return value;
        return Math.round(value / step) * step;
    }

    function formatValue(value, format, suffix) {
        var rounded = Math.round(value);

        if (format === "currency") {
            return "$" + rounded.toLocaleString();
        }

        if (format === "number") {
            return rounded.toLocaleString() + (suffix || "");
        }

        return String(rounded) + (suffix || "");
    }

    function RangeFilter(element, options) {
        this.element = element;
        this.$element = $(element);
        this.element.__gtxRangeFilter = this;
        this.options = $.extend({}, RangeFilter.defaults, this.readOptions(), options || {});
        this.$slider = this.$element.find(this.options.sliderSelector).first();
        this.$minInput = this.$element.find(this.options.minInputSelector).first();
        this.$maxInput = this.$element.find(this.options.maxInputSelector).first();
        this.$minLabel = this.findLabel("min");
        this.$maxLabel = this.findLabel("max");
        this.liveTimer = 0;
        this.isSetting = false;
        this.init();
    }

    RangeFilter.defaults = {
        sliderSelector: "[data-range-slider]",
        minInputSelector: ".inventory-range-min-input",
        maxInputSelector: ".inventory-range-max-input",
        minLabelSelector: "[data-range-value-label-min]",
        maxLabelSelector: "[data-range-value-label-max]",
        activeClass: "is-range-active",
        eventNamespace: ".gtxRangeFilter",
        inputEvent: "gtxrangefilterinput",
        changeEvent: "gtxrangefilterchange",
        live: true,
        liveDelay: 80,
        min: 0,
        max: 100,
        step: 1,
        format: "number",
        suffix: ""
    };

    RangeFilter.prototype.readOptions = function () {
        var data = this.$element.data();
        var min = parseNumber(data.rangeMin, 0);
        var max = parseNumber(data.rangeMax, min);
        var step = parseNumber(data.rangeStep, 1);

        return {
            field: data.rangeField || "",
            min: min,
            max: max,
            step: step > 0 ? step : 1,
            format: data.rangeFormat || "number",
            suffix: data.rangeSuffix || ""
        };
    };

    RangeFilter.prototype.findLabel = function (bound) {
        var field = this.options.field;
        var selector = bound === "min" ? this.options.minLabelSelector : this.options.maxLabelSelector;
        var $label = this.$element.find(selector).first();

        if ($label.length || !field) return $label;

        if (bound === "min") {
            return this.$element.find("[data-range-value-label='" + field + "-min']").first();
        }

        return this.$element
            .find("[data-range-value-label='" + field + "-max'], [data-range-value-label='" + field + "']")
            .first();
    };

    RangeFilter.prototype.init = function () {
        var values = this.normalizeValues({
            min: parseNumber(this.$minInput.val(), this.options.min),
            max: parseNumber(this.$maxInput.val(), this.options.max)
        });

        this.$element.addClass("gtx-range-filter");
        this.syncInputs(values);
        this.syncLabels(values);

        if (!this.$slider.length || !window.noUiSlider) {
            return;
        }

        this.createOrUpdateSlider(values);
    };

    RangeFilter.prototype.trackMax = function () {
        if (this.options.max > this.options.min) {
            return this.options.max;
        }

        return this.options.min + this.options.step;
    };

    RangeFilter.prototype.normalizeValues = function (values) {
        var minLimit = this.options.min;
        var maxLimit = this.options.max;
        var min = roundToStep(parseNumber(values.min, minLimit), this.options.step);
        var max = roundToStep(parseNumber(values.max, maxLimit), this.options.step);

        min = clamp(min, minLimit, maxLimit);
        max = clamp(max, minLimit, maxLimit);

        if (min > max) {
            if (values.bound === "min") {
                max = min;
            } else {
                min = max;
            }
        }

        return {
            min: min,
            max: max
        };
    };

    RangeFilter.prototype.createOrUpdateSlider = function (values) {
        var slider = this.$slider[0];
        var trackMax = this.trackMax();
        var disabled = this.options.max <= this.options.min;
        var sliderValues = [values.min, values.max];

        if (disabled) {
            sliderValues = [this.options.min, this.options.min];
        }

        if (slider.noUiSlider) {
            this.isSetting = true;
            slider.noUiSlider.updateOptions({
                step: this.options.step,
                range: {
                    min: this.options.min,
                    max: trackMax
                }
            }, false);
            slider.noUiSlider.set(sliderValues);
            this.isSetting = false;
        } else {
            window.noUiSlider.create(slider, {
                start: sliderValues,
                connect: true,
                step: this.options.step,
                range: {
                    min: this.options.min,
                    max: trackMax
                },
                behaviour: "tap-drag"
            });

            this.bindSlider();
        }

        if (disabled && slider.noUiSlider.disable) {
            slider.noUiSlider.disable();
        } else if (slider.noUiSlider.enable) {
            slider.noUiSlider.enable();
        }
    };

    RangeFilter.prototype.bindSlider = function () {
        var self = this;
        var slider = this.$slider[0];

        slider.noUiSlider.off(this.options.eventNamespace);

        slider.noUiSlider.on("update" + this.options.eventNamespace, function (values, handle) {
            var current = self.value();
            var next = self.normalizeValues({
                min: values[0],
                max: values[1],
                bound: handle === 0 ? "min" : "max"
            });

            if (next.min !== current.min || next.max !== current.max) {
                self.syncInputs(next);
            }

            self.syncLabels(next);
        });

        slider.noUiSlider.on("slide" + this.options.eventNamespace, function () {
            if (!self.options.live || self.isSetting) return;

            window.clearTimeout(self.liveTimer);
            self.liveTimer = window.setTimeout(function () {
                self.trigger(self.options.inputEvent);
            }, self.options.liveDelay);
        });

        slider.noUiSlider.on("change" + this.options.eventNamespace, function (values, handle) {
            if (self.isSetting) return;

            window.clearTimeout(self.liveTimer);

            var next = self.normalizeValues({
                min: values[0],
                max: values[1],
                bound: handle === 0 ? "min" : "max"
            });

            self.value(next, true);
            self.trigger(self.options.changeEvent);
        });
    };

    RangeFilter.prototype.syncInputs = function (values) {
        this.$minInput.val(values.min);
        this.$maxInput.val(values.max);

        this.$element.toggleClass(
            this.options.activeClass,
            values.min > this.options.min || values.max < this.options.max
        );
    };

    RangeFilter.prototype.syncLabels = function (values) {
        this.$minLabel.text(formatValue(values.min, this.options.format, this.options.suffix));
        this.$maxLabel.text(formatValue(values.max, this.options.format, this.options.suffix));
    };

    RangeFilter.prototype.trigger = function (eventName) {
        var value = this.value();

        this.$element.trigger(eventName, [value, this]);

        if (typeof window.CustomEvent === "function") {
            this.element.dispatchEvent(new window.CustomEvent(eventName, {
                bubbles: true,
                detail: {
                    value: value,
                    instance: this
                }
            }));
            return;
        }

        var event = document.createEvent("CustomEvent");
        event.initCustomEvent(eventName, true, false, {
            value: value,
            instance: this
        });
        this.element.dispatchEvent(event);
    };

    RangeFilter.prototype.value = function (next, silent) {
        if (next === undefined) {
            return {
                min: parseNumber(this.$minInput.val(), this.options.min),
                max: parseNumber(this.$maxInput.val(), this.options.max)
            };
        }

        var values = Array.isArray(next)
            ? this.normalizeValues({ min: next[0], max: next[1] })
            : this.normalizeValues(next);

        this.syncInputs(values);
        this.syncLabels(values);

        if (this.$slider.length && this.$slider[0].noUiSlider) {
            this.isSetting = true;
            this.$slider[0].noUiSlider.set([values.min, values.max]);
            this.isSetting = false;
        }

        if (!silent) {
            this.trigger(this.options.changeEvent);
        }

        return this.$element;
    };

    RangeFilter.prototype.limits = function (min, max, silent) {
        if (min === undefined) {
            return {
                min: this.options.min,
                max: this.options.max
            };
        }

        if ($.isPlainObject(min)) {
            silent = max;
            max = min.max;
            min = min.min;
        }

        this.options.min = parseNumber(min, this.options.min);
        this.options.max = parseNumber(max, this.options.max);
        this.options.step = parseNumber(this.options.step, 1);

        var values = this.normalizeValues(this.value());
        this.$element
            .attr("data-range-min", this.options.min)
            .attr("data-range-max", this.options.max)
            .data("range-min", this.options.min)
            .data("range-max", this.options.max);

        this.syncInputs(values);
        this.syncLabels(values);

        if (this.$slider.length && this.$slider[0].noUiSlider) {
            this.createOrUpdateSlider(values);
        }

        if (!silent) {
            this.trigger(this.options.changeEvent);
        }

        return this.$element;
    };

    RangeFilter.prototype.reset = function (silent) {
        return this.value({
            min: this.options.min,
            max: this.options.max
        }, silent);
    };

    RangeFilter.prototype.refresh = function () {
        this.options = $.extend({}, this.options, this.readOptions());
        this.init();
        return this.$element;
    };

    RangeFilter.prototype.destroy = function () {
        window.clearTimeout(this.liveTimer);

        if (this.$slider.length && this.$slider[0].noUiSlider) {
            this.$slider[0].noUiSlider.off(this.options.eventNamespace);
            this.$slider[0].noUiSlider.destroy();
        }

        this.element.__gtxRangeFilter = null;
        this.$element.removeData(dataKey).removeClass("gtx-range-filter " + this.options.activeClass);
    };

    $.fn[pluginName] = function (option) {
        var args = Array.prototype.slice.call(arguments, 1);
        var returnValue;

        this.each(function () {
            var $this = $(this);
            var instance = $this.data(dataKey) || this.__gtxRangeFilter;

            if (!instance) {
                instance = new RangeFilter(this, typeof option === "object" ? option : {});
                $this.data(dataKey, instance);
            }

            if (typeof option === "string") {
                if (!instance[option] || option.charAt(0) === "_") {
                    $.error("No method named " + option + " on " + pluginName);
                    return;
                }

                var result = instance[option].apply(instance, args);

                if (result !== undefined && returnValue === undefined) {
                    returnValue = result;
                }
            }
        });

        return returnValue !== undefined ? returnValue : this;
    };

    $.GtxRangeFilter = RangeFilter;

    window.ensureInventoryDualRangeSlidersReady = function () {
        $(".inventory-range-filter").gtxRangeFilter();
    };
})(jQuery, window);
