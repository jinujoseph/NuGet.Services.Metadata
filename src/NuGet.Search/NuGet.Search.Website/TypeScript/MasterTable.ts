/// <reference path="../def/jquery.d.ts" />
/// <reference path="../def/elasticsearch.jquery.d.ts" />
/// <reference path="../def/kendo.web.d.ts" />

interface IMasterTableOptions {
    tableId: string;
    index: string;
    docType: string;
    sort: string;
    rowClickEvent: (e: any) => void;
    dataBound?: (arg?: any) => void;
    columnChangedEvent: (e: any) => void;
    activeCols?: string[];
}

class MasterTable {

    public KendoUITable: any;
    public KendoUIDataSource: kendo.data.DataSource;
    public Options: IMasterTableOptions;

    constructor(options: IMasterTableOptions) {
        var that = this;

        if (!options || options.tableId == null) {
            throw new Error('A MasterTableOptions object with at least "tableId" is required.');
        }

        this.Options = options;

        var dataSource = this.KendoUIDataSource = new kendo.data.DataSource({
            error: function (e) {
                //TODO: Bung ignored these errors, should we?
                dataSource.data([]);
            },
            pageSize: 100,
            schema: {
                data: "Data",
                total: "Total"
            },
            serverFiltering: true,
            serverPaging: true,
            transport: {
                read: {
                    url: '/api/Search',
                    type: 'POST',
                    data: function () {
                        return {
                            index: options.index,
                            searchText: encodeURIComponent($("#searchBar").val().toString()),
                            docType: options.docType,
                            sort: options.sort
                        };
                    },
                    dataType: 'json',
                    contentType: "application/json",
                    cache: false
                },
                parameterMap: function (data, operation) {
                    return JSON.stringify(data);
                },
                //schema: {
                //    model: {
                //        id: "PackageId",
                //        fields: {
                //            id: { type: "string" },
                //            packageId: { type: "string" },
                //            version: { type: "string" },
                //            downloadCount: { type: "int" },
                //            packageUrl: { type: "string" },
                //        }
                //    }
                //}
            }
        });

        $(options.tableId).html("");

        var displayColumn = function (value: string, array: string[]): boolean {
            var display = $.inArray(value, array) >= 0 ? false : true;
            return display;
        }

        var kendoGrid = this.KendoUITable = $(options.tableId).kendoGrid({
            allowCopy: true,
            change: options.rowClickEvent,
            columnMenu: true,
            columnResizeHandleWidth: 8,
            columnHide: options.columnChangedEvent,
            columnShow: options.columnChangedEvent,
            columns: [
                { field: "Id", title: "Id", width: 200, hidden: displayColumn('Id', options.activeCols) },
                { field: "RunLogs[0].RunInfo.Machine", title: "Machine", width: 200, hidden: displayColumn('Machine', options.activeCols) },
                { field: "RunLogs[0].RunInfo.RunDate", title: "Date", width: 200, hidden: displayColumn('Date', options.activeCols) },
                { field: "RunLogs[0].ToolInfo.FullName", title: "Tool", width: 200, hidden: displayColumn('Tool', options.activeCols) },
                //{
                //    field: "tags",
                //    title: "Tags",
                //    attributes: {
                //        "class": "search-tag"
                //    },
                //    template: function (dataItem) {
                //        var html = "";
                //        var tags = dataItem.tags;

                //        if (tags != null) {
                //            var html = "";

                //            for (var i = 0; i < dataItem.tags.length; i++) {
                //                html += '<span class="label label-default tag-' + dataItem.tags[i] + '"><span>';
                //                html += dataItem.tags[i];
                //                html += '</span><span class="badge">X</span></span>';
                //            }
                //        }

                //        return html;
                //    },
                //    hidden: displayColumn('tags', options.activeCols)
                //},
                //{
                //    field: "disposition",
                //    title: "Disposition",
                //    template: function (dataItem) {
                //        var html = "";
                //        if (dataItem.disposition) {
                //            for (var i = 0; i < dataItem.disposition.length; i++) {
                //                html += dataItem.disposition[i];
                //            }
                //        }
                //        return html;
                //    },
                //    hidden: displayColumn('disposition', options.activeCols)
                //},
            ],
            dataBound: options.dataBound,
            dataSource: this.KendoUIDataSource,
            height: "auto",
            navigatable: true,
            pageable: {
                buttonCount: 1,
                messages: {
                    display: "Showing {0}-{1} from {2} data items."
                },
                info: true,
            },
            reorderable: true,
            resizable: true,
            selectable: true,
            scrollable: {
                virtual: true
            },
            // sortable: true,
            //excel: {
            //    allPages: true,
            //    fileName: "packages.xlsx"
            //},
            //toolbar: ["excel"]
        });
    }
}