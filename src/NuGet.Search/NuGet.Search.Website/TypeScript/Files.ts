/// <reference path="../def/jquery.d.ts" />
/// <reference path="../def/jsrender.d.ts" />
/// <reference path="../def/elasticsearch.jquery.d.ts" />
/// <reference path="../def/resizeBar.d.ts" />

var sarifPageController: SarifPageController;
declare var activeCols: string[];

$(function () {
    sarifPageController = new SarifPageController();
});

class SarifPageController {

    public SarifTable: MasterTable;

    private _docType: string = 'resultlog';
    private _pageTitle: string = 'NuGet Indexer';
    private _activeIndex: string;
    private _activeSarifViewTab: string = 'Logs';
    private _commonQuery: boolean = false;

    private _indexToUrlMap = {
        "prod": "http://atp-notavailable",
    };

    constructor() {

        var controller: SarifPageController = this;

        this._registerEventHandlers();

        // Initialize the splitter
        $(".main > div").eq(0).resizerBar("ew");

        controller._activeIndex = $('#ElasticSearchIndex option:selected').val();

        controller._initMasterTable();
        controller._initKendoControls();

        controller._updateUrl();
    }

    private _initMasterTable(): void {
        var controller: SarifPageController = this;

        var _columnChangeEvent = function (e: any) {
            controller._updateUrl();
        }

        // Create the master table
        var masterTableOptions: IMasterTableOptions = {
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
                    success: function (responseText: string): void {
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
                        //$(".js-sarif").html(htmlOutput);

                        var $sarifTabs = $(".js-sarifTabs", '#js-sarifs');
                        $sarifTabs.kendoTabStrip({
                            animation: false,
                        });

                        // BUGBUG: Sarif Details active tab functionality disabled
                        //$('.js-sarifTabs > ul > li').click(function () {
                        //    controller._activeSarifViewTab = $(this).text();
                        //});

                        $('.collapsible').on('click', function () {
                            $(this).toggleClass('collapsed');
                        })
                    },
                    error: function (err: any): void {
                        $('.js-sarif', '#js-sarifs').empty().append("Could not find _id: " + _documentId);
                    }
                });
            },
            dataBound: function (arg) {
                // Bind remove tag click events
                $("td.search-tag").each(function (index, element) {
                    var cell = $(element);
                    var className = "";
                    
                    if (cell.children(".tag-fp").length > 0) {
                        className = "row-fp";
                    } else if (cell.children(".tag-tp").length > 0) {
                        className = "row-tp";
                    } else if (cell.children(".tag-fn").length > 0) {
                        className = "row-fn";
                    } else if (cell.children(".tag-tn").length > 0) {
                        className = "row-tn";
                    } else if (cell.children(".tag-s").length > 0) {
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
        } else {
            masterTableOptions.activeCols = defaultCols;
        }        

        this.SarifTable = new MasterTable(masterTableOptions);
    }

    private _registerEventHandlers(): void {
        var controller = this;
        var timeout = 200;
        var timeoutSet = false;

        $('#searchBar').keyup(function (e: JQueryKeyEventObject): void {
            // Throttle queries
            if (!timeoutSet) {
                timeoutSet = true;
                setTimeout(function () {
                    timeoutSet = false;
                    controller._updateUrl();
                    controller.SarifTable.KendoUIDataSource.fetch();
                    controller._emptySarifDetails();

                    if (controller._commonQuery) {
                        // Remove commonQuery selection
                        controller._commonQuery = false;
                    } else {
                        $('#CommonQueries').data('kendoDropDownList').select(0);
                    }
                }, timeout);
            }
        });

        $('#searchBtn').on('click', function () {
            $('#searchBar').keyup();
        });
    }

    private _initKendoControls(): void {
        var controller = this;

        function replaceSearch(query) {
            $('#searchBar').val(query).keyup();
        }

        //Add to toolbar
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
                } else {
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
    }

    private _updateUrl(): void {
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
    }

    private _emptySarifDetails(): void {
        $('.js-sarif', '#js-sarifs').empty();
    }
}

//interface ITagInfo {
//    Index: string;
//    DocumentId: string;
//    DocType: string;
//    Tags: string[];
//}

//interface IDispositionInfo {
//    Index: string;
//    DocumentId: string;
//    DocType: string;
//    Disposition: string;
//}

//interface ITagAndDispositionInfo {
//    Index: string;
//    Query: string;
//    DocType: string;
//    Tags: string[];
//    Disposition: string;
//}

//interface ISubmission {
//    submissionId: string;
//    submissionToke: string;
//    analysisWorkflow: string;
//    source: string;
//    submitter: string;
//    sharingMode: string;
//    priority: number;
//    context: string;
//    accessKey: string;
//    createdOn: string;
//    completedOn: string;
//    state: string;
//    summaryType: string;
//    summaryText: string;
//    inputFileName: string;
//    inputFileMd5: string;
//    inputFileSha1: string;
//    inputFileExt: string;
//    tasks: Array<ISubmissionTask>;
//    reasons: string;
//}

//interface ISubmissionTask {
//    taskId: string;
//    taskToken: string;
//    subsystem: string;
//    toolSet: string;
//    toolName: string;
//    toolVersion: string;
//    inputFile: string;
//    inputFileMd5: string;
//    inputFileSha1: string;
//    inputFileExt: string;
//    processingParameters: string;
//    processingHost: string;
//    processingTime: number;
//    summaryIdentifierConfidence: number;
//    summaryTags: Array<string>;
//    sharingMode: string;
//    priority: number;
//    createdOn: string;
//    completedOn: string;
//    state: string;
//    summaryType: string;
//    summaryText: string;
//    level: number;
//    findings: Array<ISubmissionFinding>;
//    etwEvents: Array<any>;
//    etwLog: string;
//}

//interface ISubmissionFinding {
//    findingName: string;
//    param1?: string;
//    param2?: string;
//    param3?: string;
//    param4?: string;
//    param5?: string;
//    param6?: string;
//    param7?: string;
//    param8?: string;
//    param9?: string;
//    param10?: string;
//}

interface ISubmissionDataProviderOptions {
    index: string;
    host: string;
}