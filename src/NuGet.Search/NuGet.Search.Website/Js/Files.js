var sarifPageController;
$(function () {
    sarifPageController = new SarifPageController();
});
var SarifPageController = (function () {
    function SarifPageController() {
        this._docType = 'resultlog';
        this._pageTitle = 'NuGet Indexer';
        this._activeSarifViewTab = 'Logs';
        this._commonQuery = false;
        this._indexToUrlMap = {
            "prod": "http://atp-notavailable",
        };
        var controller = this;
        this._registerEventHandlers();
        $(".main > div").eq(0).resizerBar("ew");
        controller._activeIndex = $('#ElasticSearchIndex option:selected').val();
        controller._initMasterTable();
        controller._initKendoControls();
        controller._updateUrl();
    }
    SarifPageController.prototype._initMasterTable = function () {
        var controller = this;
        var _columnChangeEvent = function (e) {
            controller._updateUrl();
        };
        var masterTableOptions = {
            tableId: '#submissions',
            index: controller._activeIndex,
            docType: 'resultlog',
            sort: 'runLogs.runInfo.runDate:desc',
            rowClickEvent: function (e) {
                var _documentId = this.select()[0].firstChild.textContent;
                var _url = '/api/Documents/GetJson/' + encodeURIComponent(controller._activeIndex) + '/' + encodeURI(_documentId);
                $.ajax({
                    url: _url,
                    type: 'GET',
                    dataType: 'json',
                    contentType: "application/json; charset=utf-8",
                    success: function (responseText) {
                        var data = JSON.parse(responseText);
                        var template = $.templates("#sarifTemplate");
                        if (data.summaryType) {
                            data.summaryType = data.summaryType.toUpperCase();
                        }
                        data.IndexName = $('#ElasticSearchIndex option:selected').text();
                        data.SourceUrl = controller._indexToUrlMap[$('#ElasticSearchIndex option:selected').text()];
                        data.ActiveSarifViewTab = controller._activeSarifViewTab;
                        var htmlOutput = template.render(data);
                        $('.js-sarif', '#js-sarifs').empty().append(htmlOutput);
                        var $sarifTabs = $(".js-sarifTabs", '#js-sarifs');
                        $sarifTabs.kendoTabStrip({
                            animation: false,
                        });
                        $('.collapsible').on('click', function () {
                            $(this).toggleClass('collapsed');
                        });
                    },
                    error: function (err) {
                        $('.js-sarif', '#js-sarifs').empty().append("Could not find _id: " + _documentId);
                    }
                });
            },
            dataBound: function (arg) {
                $("td.search-tag").each(function (index, element) {
                    var cell = $(element);
                    var className = "";
                    if (cell.children(".tag-fp").length > 0) {
                        className = "row-fp";
                    }
                    else if (cell.children(".tag-tp").length > 0) {
                        className = "row-tp";
                    }
                    else if (cell.children(".tag-fn").length > 0) {
                        className = "row-fn";
                    }
                    else if (cell.children(".tag-tn").length > 0) {
                        className = "row-tn";
                    }
                    else if (cell.children(".tag-s").length > 0) {
                        className = "row-s";
                    }
                    cell.parent().addClass(className);
                });
            },
            columnChangedEvent: _columnChangeEvent
        };
        var defaultCols = [
            'Machine',
            'Date',
            'Tool',
        ];
        if (activeCols && activeCols.length > 0) {
            masterTableOptions.activeCols = activeCols;
        }
        else {
            masterTableOptions.activeCols = defaultCols;
        }
        this.SarifTable = new MasterTable(masterTableOptions);
    };
    SarifPageController.prototype._registerEventHandlers = function () {
        var controller = this;
        var timeout = 200;
        var timeoutSet = false;
        $('#searchBar').keyup(function (e) {
            if (!timeoutSet) {
                timeoutSet = true;
                setTimeout(function () {
                    timeoutSet = false;
                    controller._updateUrl();
                    controller.SarifTable.KendoUIDataSource.fetch();
                    controller._emptySarifDetails();
                    if (controller._commonQuery) {
                        controller._commonQuery = false;
                    }
                    else {
                        $('#CommonQueries').data('kendoDropDownList').select(0);
                    }
                }, timeout);
            }
        });
        $('#searchBtn').on('click', function () {
            $('#searchBar').keyup();
        });
    };
    SarifPageController.prototype._initKendoControls = function () {
        var controller = this;
        function replaceSearch(query) {
            $('#searchBar').val(query).keyup();
        }
        $('#CommonQueries').kendoDropDownList({
            optionLabel: ' ',
            dataTextField: 'text',
            dataValueField: 'value',
            dataSource: [
                {
                    text: '',
                    value: 'None'
                },
                {
                    text: 'Todays Logs',
                    value: 'runLogs.runInfo.runDate:[now-1d TO now+9h]'
                },
                {
                    text: 'Production Server',
                    value: 'runLogs.runInfo.machine:NUGETINDEXER'
                },
                {
                    text: 'Error Runs',
                    value: 'runLogs.results.properties.Level:Error'
                },
            ],
            change: function (e) {
                var query = e.sender.value();
                if (query == 'None') {
                    controller._commonQuery = false;
                }
                else {
                    controller._commonQuery = true;
                    replaceSearch(query);
                }
            }
        });
        $('#ElasticSearchIndex').kendoDropDownList({
            optionLabel: ' ',
            dataTextField: 'text',
            dataValueField: 'value',
            change: function (e) {
                controller._activeIndex = e.sender.value();
                controller._initMasterTable();
                controller._updateUrl();
                controller._emptySarifDetails();
            }
        });
    };
    SarifPageController.prototype._updateUrl = function () {
        var params = '';
        var index = $('#ElasticSearchIndex option:selected').text();
        if (index.length > 0) {
            params += 'i=' + index;
        }
        var query = $('#searchBar').val();
        if (query.length > 0) {
            params += params.length > 0 ? '&' : '';
            params += 'q=' + encodeURIComponent(query);
        }
        var cols = $(this.SarifTable.Options.tableId).data('kendoGrid').columns;
        if (cols != null && cols.length > 0) {
            params += params.length > 0 ? '&' : '';
            params += 'cols=';
            var colsParams = '';
            for (var i = 0; i < cols.length; i++) {
                var col = cols[i];
                if (!col.hidden) {
                    colsParams += (colsParams.length > 0 ? ',' : '');
                    colsParams += cols[i].title;
                }
            }
            params += colsParams;
        }
        var url = window.location.protocol + '//';
        url += window.location.host;
        url += window.location.pathname;
        if (params.length > 0) {
            url += '?' + params;
        }
        window.history.replaceState(null, this._pageTitle, url);
    };
    SarifPageController.prototype._emptySarifDetails = function () {
        $('.js-sarif', '#js-sarifs').empty();
    };
    return SarifPageController;
})();
//# sourceMappingURL=Files.js.map