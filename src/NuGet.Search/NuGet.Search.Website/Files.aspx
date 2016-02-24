<%@ Page Title="" Language="C#" MasterPageFile="~/NuGetSearch.Search.Master" AutoEventWireup="true" CodeBehind="Files.aspx.cs" Inherits="NuGet.Search.Website.Files" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
NuGet Indexer
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdditionalHeaders" runat="server">
    <style>


        /* Responsive layout for 4 toolbar items */
        .queryStringToolbar.k-toolbar {
            font-size: 14px;
        }
        /* Responsive layout for 4 toolbar items */
        @media all and (max-width: 1315px) {
            .queryStringToolbar.k-toolbar {
                font-size: 13px;
            }
        }

        @media all and (max-width: 1245px) {
            .queryStringToolbar.k-toolbar {
                font-size: 12px;
            }
        }
        @media all and (max-width: 1165px) {
            .queryStringToolbar.k-toolbar {
                font-size: 11px;
            }
        }
        @media all and (max-width: 1080px) {
            .queryStringToolbar.k-toolbar {
                font-size: 14px;
            }
        }
    </style>
    <%
        var activeColsParams = Server.HtmlEncode(Request.QueryString["cols"]);
        var activeColsString = "";
        if (!String.IsNullOrWhiteSpace(activeColsParams)) {
            var activeCols = activeColsParams.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (activeCols.Length > 0)
            {
                foreach (var col in activeCols)
                {
                    activeColsString += activeColsString.Length > 0 ? "," : "";
                    activeColsString += "\"" + col + "\"";
                }
            }
        }
    %>
    <script type="text/javascript" src="js/MasterTable.js"></script>
    <script type="text/javascript" src="js/Files.js"></script>
    <script type="text/javascript">
        var activePage = "#navbarFiles";
        var activeCols = [<%=activeColsString%>];
    </script>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="MainContent" runat="server">           

<div id="submissions"></div>
<div id="js-sarifs">
    <div class="js-sarif"></div>
</div>

<!--#include file="JsViews\FileView.html"-->

<ul id="contextMenu" style="display:none">
    <li>False-Positive</li>
    <li>False-Negative</li>
    <li class="k-separator"></li>
    <li>True-Positive</li>
    <li>True-Negative</li>
    <li class="k-separator"></li>
    <li>Suspicious</li>
    <%--<li>Label
        <ul>
            <li>None</li>
            <li class="k-separator"></li>
            <li>Important</li>
            <li>Work</li>
            <li>Personal</li>
            <li class="k-separator"></li>
            <li>New Label</li>
        </ul>
    </li>--%>
</ul>

</asp:Content>