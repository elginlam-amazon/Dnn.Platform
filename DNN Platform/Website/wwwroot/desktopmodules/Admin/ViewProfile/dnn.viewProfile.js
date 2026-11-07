/*
 * Knockout bootstrap of the ViewProfile module.
 *
 * This is the inline <script> block of ViewProfile.ascx, which a Razor component may not emit. The
 * per-module ProfileViewModelModule<ModuleId> function became the view-model factory below, and the
 * values the block interpolated server side (the six localized captions, the ko.observable per
 * profile property and the matching <Name>Text label, plus Email/EmailText) now travel as JSON in
 * the data-view-model attribute ViewProfile.razor renders. Everything else - the Location computed
 * observable, the pass that back-fills the observables and the *Text labels the template's
 * data-bind expressions reference but the server did not emit, the Visible flag and the guarded
 * ko.applyBindings - is unchanged.
 */
(function ($) {
    function createViewModel(model, $container) {
        var self = this;

        var texts = model.texts || {};
        Object.keys(texts).forEach(function (name) {
            self[name] = texts[name];
        });

        var observables = model.observables || {};
        Object.keys(observables).forEach(function (name) {
            self[name] = ko.observable(observables[name]);
        });

        var labels = model.labels || {};
        Object.keys(labels).forEach(function (name) {
            self[name] = labels[name];
        });

        self.Location = ko.computed(function () {
            var city = typeof self.City === "function" ? self.City() : "";
            var region = typeof self.Region === "function" ? self.Region() : "";
            var location = (city != null) ? city : '';
            if (location != '' && region != null && region != '') {
                location += ', ';
            }
            if (region != null) {
                location += region;
            }

            return location;
        });

        // process all binded functions to apply empty values on not exist properties.
        $container.find('*[data-bind]').each(function () {
            var binding = $(this).attr('data-bind');

            $.each(binding.match(/\w+?\(\)/gi), function (index, name) {
                name = name.replace('()', '');
                if (typeof self[name] === "undefined") {
                    self[name] = ko.observable('');
                }
            });

            $.each(binding.split(','), function (index, part) {
                var parts = part ? part.split(':') : [];
                if (parts.length === 2) {
                    var name = parts[1].trim().replace('()', '');
                    if (name && name.length > 4 && name.substr(name.length - 4, 4) === "Text" && typeof self[name] === "undefined") {
                        self[name] = '';
                    }
                }
            });
        });

        self.Visible = true;
    }

    $(document).ready(function () {
        $('.dnnViewProfileViewModel').each(function () {
            var $island = $(this);
            var $container = $('#' + $island.attr('data-container'));
            if (!$container.length) {
                return;
            }

            var model;
            try {
                model = JSON.parse($island.attr('data-view-model'));
            } catch (e) {
                return;
            }

            try {
                ko.applyBindings(new createViewModel(model, $container), $container[0]);
            } catch (e) {

            }
        });
    });
})(jQuery);
