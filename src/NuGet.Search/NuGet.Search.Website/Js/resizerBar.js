$(function ($) {
    /*
     * The resizerBar creates a bar that divides 2 adjacent elements, either vertically or horizontally. 
     * It will add the resizer bar to the right of the matched element(s) if the direction is set to "ew" (default)
     * or it will add the resizer bar to bottom of the matched element(s) if the direction is set to "ns".
     * 
     * @param {String || Object}
     *  As String: "ew", "ns"
     *  As Object: {direction, backgroundColor, width, fadeIn}
     */
    $.fn.resizerBar = function (options) {

        if (typeof options == "string") {
            options = {
                direction: options
            }
        }

        var settings = $.extend({
            // These are the defaults.
            direction: "ew",
            fadeIn: false,
            width: 6,
            offset: 0,
            pane1: {
                "min-size": 50
            },
            pane2: {
                "min-size": 50
            }
        }, options);

        this.each(function (index) {
            var $next = $(this).next();
            var $parent = $(this).parent()[0].tagName == "BODY" ? $(this).add($next).wrapAll("<div style='overflow:auto'>").parent() : $(this).parent();

            var $parentPos = $parent.css("position");

            if ($parentPos != "fixed" && $parentPos != "absolute" && $parentPos != "relative") {
                $parent.css("position", "relative")
            }

            var css = {
                position: "absolute",
                //display: "none",
                "box-sizing": "content-box",
                "z-index": 2
            }

            if (settings.direction == "ew") {
                var $el = $("<div class='resizerBar ew' />").css(
                    $.extend(css, {
                        cursor: "col-resize",
                        height: $parent.innerHeight(),
                        width: settings.width + "px",
                        background: "#3d464d",
                        top: 0,
                        left: $(this).innerWidth() - (settings.width / 2) + settings.offset,
                        transform: "translateX(-50%)"
                    })
                );

                $el.on('mousedown', function (e) {
                    var $resizerBar = $(this);
                    var $parentOffsetLeft = $(e.target).parent().offset().left;
                    var $parentWidth = $resizerBar.parent().width();

                    var handlers = {
                        mousemove: function (e) {
                            //position the resizerBar
                            var x = (e.pageX - $parentOffsetLeft) - (settings.width / 2);

                            $resizerBar.css({
                                left: (x > settings.pane1["min-size"]) ? ((x < $parentWidth - settings.pane2["min-size"]) ? x + settings.offset + 'px' : $parentWidth - settings.pane2["min-width"]) : settings.pane1["min-width"]
                            });
                            event.preventDefault();
                        },
                        mouseup: function (e) {
                            //Set the widths of the columns
                            $(this).off(handlers);

                            var prevWidth = e.pageX - $parentOffsetLeft;
                            prevWidth = prevWidth > settings.pane1["min-size"] ? prevWidth : settings.pane1["min-size"];
                            
                            var nextWidth = $parentWidth - prevWidth;
                            nextWidth = nextWidth > settings.pane2["min-size"] ? nextWidth : settings.pane2["min-size"];

                            //Use flex-basis for flex layout
                            $resizerBar.prev().css('flex-basis', prevWidth);
                            $resizerBar.next().css('flex-basis', nextWidth);
                            $resizerBar.trigger("resize-ew:complete");

                            //for layouts other than flex
                            //$resizerBar.prev().width(prevWidth);
                            //$resizerBar.next().width($parentWidth - prevWidth);
                        }
                    };
                    $(document).on(handlers);
                });

                $el.on({
                    mouseover: function () {
                        $(this).addClass("growResizerBar");
                        //To do: add quick collapse
                        //&#9658; right
                        //&#9664; left
                    },
                    mouseout: function () {
                        $(this).removeClass("growResizerBar");
                    }
                });

            } else if (settings.direction == "ns") {
                var $el = $("<div class='resizerBar ns' />").css(
                    $.extend(css, {
                        cursor: "row-resize",
                        width: $parent.innerWidth(),
                        height: settings.width + "px",
                        background: "#3d464d",
                        top: $(this).innerHeight() - (settings.width / 2),
                        left: 0
                    })
                );

                $el.on('mousedown', function (e) {
                    var $resizerBar = $(this);
                    var $parentOffsetTop = $(e.target).parent().offset().top;
                    var $parentHeight = $resizerBar.parent().height();

                    var handlers = {
                        mousemove: function (e) {
                            //position the resizerBar
                            var y = (e.pageY - $parentOffsetTop) - (settings.width / 2);

                            $resizerBar.css({
                                top: (y > 0) ? (y < $parentHeight) ? y + 'px' : $parentHeight : 0
                            });
                            event.preventDefault();
                        },
                        mouseup: function (e) {
                            //Set the widths of the rows
                            $(this).off(handlers);
                            var prevHeight = e.pageY - $parentOffsetTop;
                            //Use flex-basis for flex layout
                            $resizerBar.prev().css('flex-basis', prevHeight);
                            $resizerBar.next().css('flex-basis', $parentHeight - prevHeight);
                            $resizerBar.trigger("resize-ns:complete");

                            //for layouts other than flex
                            //$resizerBar.prev().height(prevHeight);
                            //$resizerBar.next().height($parentHeight - prevHeight);
                        }
                    };
                    $(document).on(handlers);
                });

                $el.on({
                    mouseover: function () {
                        $(this).addClass("growResizerBar");
                        //To do: add quick collapse
                        //&#9658; right
                        //&#9664; left
                    },
                    mouseout: function () {
                        $(this).removeClass("growResizerBar");
                    }
                });

            } else {
                throw Error(["You need a valid resizerBar direction!"]);
            }

            $(this).after($el);

            //if (settings.fadeIn) {
            //    $el.fadeIn();
            //} else {
            //    $el.show();
            //}

        })

        return this;
    };

    //To do: register only once by saving a registered flag on the element with a data- and save a reference to the object. 
    $(window).resize(function () {
        resetResizerBar()
    })

    var throttleStarted = false;
    function resetResizerBar() {
        if (!throttleStarted) {
            throttleStarted = true;
            setTimeout(function () {
                $(".resizerBar.ew").each(function () {
                    $(this).css({
                        height: $(this).parent().innerHeight(), // - ($(this).width()/2),
                        left: $(this).prev().innerWidth()// + settings.offset
                    });
                })
                $(".resizerBar.ns").each(function () {
                    $(this).css({
                        width: $(this).parent().innerWidth() - $(this).height(),
                        top: $(this).prev().innerHeight()
                    });
                })

                throttleStarted = false;
            }, 60);
        }
    }

}(jQuery));
