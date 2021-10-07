﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace Radzen.Blazor
{
    public partial class RadzenDataGrid<TItem> : PagedDataBoundComponent<TItem>
    {
        #if NET5
        internal void SetAllowVirtualization(bool allowVirtualization)
        {
            AllowVirtualization = allowVirtualization;
        }

        internal Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TItem> virtualize;

        public Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TItem> Virtualize
        {
            get
            {
                return virtualize;
            }
        }

        List<TItem> virtualDataItems = new List<TItem>();

        private async ValueTask<Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderResult<TItem>> LoadItems(Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderRequest request)
        {
            var view = AllowPaging ? PagedView : View;
            var totalItemsCount = LoadData.HasDelegate ? Count : view.Count();
            var top = totalItemsCount > request.Count ? Math.Min(request.Count, totalItemsCount - request.StartIndex) : PageSize;

            if(top <= 0)
            {
                top = PageSize;
            }

            if (LoadData.HasDelegate)
            {
                var orderBy = GetOrderBy();

                Query.Skip = request.StartIndex;
                Query.Top = top;
                Query.OrderBy = orderBy;

                var filterString = columns.ToFilterString<TItem>();
                Query.Filter = filterString;

                await LoadData.InvokeAsync(new Radzen.LoadDataArgs() { Skip = request.StartIndex, Top = top, OrderBy = orderBy, Filter = IsOData() ? columns.ToODataFilterString<TItem>() : filterString });
            }

            virtualDataItems = (LoadData.HasDelegate ? Data : itemToInsert != null ? (new[] { itemToInsert }).Concat(view.Skip(request.StartIndex).Take(top)) : view.Skip(request.StartIndex).Take(top)).ToList();

            return new Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderResult<TItem>(virtualDataItems, totalItemsCount);
        }
    #endif
        RenderFragment DrawRows(IList<RadzenDataGridColumn<TItem>> visibleColumns)
        {
            return new RenderFragment(builder =>
            {
    #if NET5
                if (AllowVirtualization)
                {
                    builder.OpenComponent(0, typeof(Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TItem>));
                    builder.AddAttribute(1, "ItemsProvider", new Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderDelegate<TItem>(LoadItems));
                    builder.AddAttribute(2, "ChildContent", (RenderFragment<TItem>)((context) =>
                    {
                        return (RenderFragment)((b) =>
                        {
                            b.OpenComponent<RadzenDataGridRow<TItem>>(3);
                            b.AddAttribute(4, "Columns", visibleColumns);
                            b.AddAttribute(5, "Grid", this);
                            b.AddAttribute(6, "TItem", typeof(TItem));
                            b.AddAttribute(7, "Item", context);
                            b.AddAttribute(8, "InEditMode", IsRowInEditMode(context));
                            b.AddAttribute(9, "Index", virtualDataItems.IndexOf(context));

                            if (editContexts.ContainsKey(context))
                            {
                                b.AddAttribute(10, nameof(RadzenDataGridRow<TItem>.EditContext), editContexts[context]);
                            }

                            b.SetKey(context);
                            b.CloseComponent();
                        });
                    }));

                    builder.AddComponentReferenceCapture(8, c => { virtualize = (Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TItem>)c; });

                    builder.CloseComponent();
                }
                else
                {
                    DrawGroupOrDataRows(builder, visibleColumns);
                }
    #else
                DrawGroupOrDataRows(builder, visibleColumns);
    #endif
            });
        }

        internal void DrawGroupOrDataRows(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, IList<RadzenDataGridColumn<TItem>> visibleColumns)
        {
            if (groups.Any())
            {
                foreach (var group in GroupedPagedView)
                {
                    builder.OpenComponent(0, typeof(RadzenDataGridGroupRow<TItem>));
                    builder.AddAttribute(1, "Columns", visibleColumns);
                    builder.AddAttribute(3, "Grid", this);
                    builder.AddAttribute(5, "GroupResult", group);
                    builder.AddAttribute(6, "Builder", builder);
                    builder.CloseComponent();
                }
            }
            else
            {
                int i = 0;
                foreach (var item in PagedView)
                {
                    builder.OpenComponent<RadzenDataGridRow<TItem>>(0);
                    builder.AddAttribute(1, "Columns", visibleColumns);
                    builder.AddAttribute(2, "Index", i);
                    builder.AddAttribute(3, "Grid", this);
                    builder.AddAttribute(4, "TItem", typeof(TItem));
                    builder.AddAttribute(5, "Item", item);
                    builder.AddAttribute(6, "InEditMode", IsRowInEditMode(item));

                    if (editContexts.ContainsKey(item))
                    {
                        builder.AddAttribute(7, nameof(RadzenDataGridRow<TItem>.EditContext), editContexts[item]);
                    }

                    builder.CloseComponent();
                    i++;
                }
            }
        }   

        IEnumerable<GroupResult> _groupedPagedView;
        public IEnumerable<GroupResult> GroupedPagedView
        {
            get
            {
                if(_groupedPagedView == null)
                {
                    _groupedPagedView = PagedView.GroupByMany(groups.Select(g => g.Property).ToArray()).ToList();
                 }
                return _groupedPagedView;
            }
        }

        internal string getFrozenColumnClass(RadzenDataGridColumn<TItem> column, IList<RadzenDataGridColumn<TItem>> visibleColumns)
        {
            return column.Frozen ? "rz-frozen-cell" : "";
        }

        protected string DateFilterOperatorStyle(RadzenDataGridColumn<TItem> column, FilterOperator value)
        {
            return column.GetFilterOperator() == value ?
                "rz-listbox-item  rz-state-highlight" :
                "rz-listbox-item ";
        }

        protected void OnFilterKeyPress(EventArgs args, RadzenDataGridColumn<TItem> column)
        {
            Debounce(() => DebounceFilter(column), FilterDelay);
        }

        async Task DebounceFilter(RadzenDataGridColumn<TItem> column)
        {
            var inputValue = await JSRuntime.InvokeAsync<string>("Radzen.getInputValue", getFilterInputId(column));
            if (!object.Equals(column.GetFilterValue(), inputValue))
            {
                await InvokeAsync(() => { OnFilter(new ChangeEventArgs() { Value = inputValue }, column); });
            }
        }

        protected void ApplyDateFilterByFilterOperator(RadzenDataGridColumn<TItem> column, FilterOperator filterOperator)
        {
            column.SetFilterOperator(filterOperator);
        }

        private readonly List<RadzenDataGridColumn<TItem>> columns = new List<RadzenDataGridColumn<TItem>>();

        [Parameter]
        public RenderFragment Columns { get; set; }

        internal void AddColumn(RadzenDataGridColumn<TItem> column)
        {
            if (!columns.Contains(column))
            {
                columns.Add(column);

                var descriptor = sorts.Where(d => d.Property == column?.GetSortProperty()).FirstOrDefault();
                if (descriptor == null && column.SortOrder.HasValue)
                {
                    descriptor = new SortDescriptor() { Property = column.Property, SortOrder = column.SortOrder.Value };
                    sorts.Add(descriptor);
                }
            }
        }

        internal void RemoveColumn(RadzenDataGridColumn<TItem> column)
        {
            if (columns.Contains(column))
            {
                columns.Remove(column);
                if (!disposed)
                {
                    try { InvokeAsync(StateHasChanged); } catch { }
                }
            }
        }

        string getFilterInputId(RadzenDataGridColumn<TItem> column)
        {
            return string.Join("", $"{UniqueID}".Split('.')) + column.GetFilterProperty();
        }

        string getFilterDateFormat(RadzenDataGridColumn<TItem> column)
        {
            if (column != null && !string.IsNullOrEmpty(column.FormatString))
            {
                var formats = column.FormatString.Split(new char[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
                if (formats.Length > 0)
                {
                    var format = formats[0].Trim().Split(':');
                    if (format.Length > 1)
                    {
                        return format[1].Trim();
                    }
                }
            }

            return FilterDateFormat;
        }

        RenderFragment DrawNumericFilter(RadzenDataGridColumn<TItem> column, bool force = true, bool isFirst = true)
        {
            return new RenderFragment(builder =>
            {
                var type = Nullable.GetUnderlyingType(column.FilterPropertyType) != null ?
                    column.FilterPropertyType : typeof(Nullable<>).MakeGenericType(column.FilterPropertyType);

                var numericType = typeof(RadzenNumeric<>).MakeGenericType(type);

                builder.OpenComponent(0, numericType);

                builder.AddAttribute(1, "Value", isFirst ? column.GetFilterValue() : column.GetSecondFilterValue());
                builder.AddAttribute(2, "Style", "width:100%");

                Action<object> action;
                if (force)
                {
                    action = args => OnFilter(new ChangeEventArgs() { Value = args }, column, isFirst);
                }
                else
                {
                    action = args => column.SetFilterValue(args, isFirst);
                }

                var eventCallbackGenericCreate = typeof(NumericFilterEventCallback).GetMethod("Create").MakeGenericMethod(type);
                var eventCallbackGenericAction = typeof(NumericFilterEventCallback).GetMethod("Action").MakeGenericMethod(type);

                builder.AddAttribute(3, "Change", eventCallbackGenericCreate.Invoke(this,
                    new object[] { this, eventCallbackGenericAction.Invoke(this, new object[] { action }) }));

                if(FilterMode == FilterMode.Advanced)
                {
                    builder.AddAttribute(4, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, args => {
                        var value = $"{args.Value}";
                        column.SetFilterValue(!string.IsNullOrWhiteSpace(value) ? Convert.ChangeType(value, Nullable.GetUnderlyingType(type)) : null, isFirst);
                    } ));
                }

                builder.CloseComponent();
            });
        }

        protected void OnFilter(ChangeEventArgs args, RadzenDataGridColumn<TItem> column, bool force = false, bool isFirst = true)
        {
            string property = column.GetFilterProperty();
            if (AllowFiltering && column.Filterable)
            {
                if (!object.Equals(isFirst ? column.GetFilterValue() : column.GetSecondFilterValue(), args.Value) || force)
                {
                    column.SetFilterValue(args.Value, isFirst);
                    skip = 0;
                    CurrentPage = 0;

                    if (LoadData.HasDelegate && IsVirtualizationAllowed())
                    {
                        Data = null;
                    }

                    InvokeAsync(Reload);
                }
            }
        }


        public IList<RadzenDataGridColumn<TItem>> ColumnsCollection
        {
            get
            {
                return columns;
            }
        }

        private string getFilterIconCss(RadzenDataGridColumn<TItem> column)
        {
            var additionalStyle = column.GetFilterValue() != null || column.GetSecondFilterValue() != null ? "rz-grid-filter-active" : "";
            return $"rzi rz-grid-filter-icon {additionalStyle}";
        }

        protected void OnSort(EventArgs args, RadzenDataGridColumn<TItem> column)
        {
            if (AllowSorting && column.Sortable)
            {
                var property = column.GetSortProperty();
                if (!string.IsNullOrEmpty(property))
                {
                    OrderBy(property);
                }
                else
                {
                    SetColumnSortOrder(column);

                    if (LoadData.HasDelegate && IsVirtualizationAllowed())
                    {
                        Data = null;
                    }

                    InvokeAsync(Reload);
                }
            }
        }

        protected async Task ClearFilter(RadzenDataGridColumn<TItem> column, bool closePopup = false)
        {
            if (closePopup)
            {
                await JSRuntime.InvokeVoidAsync("Radzen.closePopup", $"{PopupID}{column.GetFilterProperty()}");
            }
            column.SetFilterValue(null);
            column.SetFilterValue(null, false);
            column.SetFilterOperator(null);
            column.SetSecondFilterOperator(null);

            skip = 0;
            CurrentPage = 0;

            if (LoadData.HasDelegate && IsVirtualizationAllowed())
            {
                Data = null;
            }

            await InvokeAsync(Reload);
        }

        protected async Task ApplyFilter(RadzenDataGridColumn<TItem> column, bool closePopup = false)
        {
            if (closePopup)
            {
                await JSRuntime.InvokeVoidAsync("Radzen.closePopup", $"{PopupID}{column.GetFilterProperty()}");
            }
            OnFilter(new ChangeEventArgs() { Value = column.GetFilterValue() }, column, true);
        }

        internal IReadOnlyDictionary<string, object> CellAttributes(TItem item, RadzenDataGridColumn<TItem> column)
        {
            var args = new Radzen.DataGridCellRenderEventArgs<TItem>() { Data = item, Column = column };

            if (CellRender != null)
            {
                CellRender(args);
            }

            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(args.Attributes);
        }

        internal Dictionary<int, int> rowSpans = new Dictionary<int, int>();

        [Parameter]
        public LogicalFilterOperator LogicalFilterOperator { get; set; } = LogicalFilterOperator.And;

        [Parameter]
        public FilterMode FilterMode { get; set; } = FilterMode.Advanced;

        [Parameter]
        public DataGridExpandMode ExpandMode { get; set; } = DataGridExpandMode.Multiple;

        [Parameter]
        public DataGridEditMode EditMode { get; set; } = DataGridEditMode.Multiple;

        [Parameter]
        public string FilterText { get; set; } = "Filter";

        [Parameter]
        public string AndOperatorText { get; set; } = "And";

        [Parameter]
        public string OrOperatorText { get; set; } = "Or";

        [Parameter]
        public string ApplyFilterText { get; set; } = "Apply";

        [Parameter]
        public string ClearFilterText { get; set; } = "Clear";

        [Parameter]
        public string EqualsText { get; set; } = "Equals";

        [Parameter]
        public string NotEqualsText { get; set; } = "Not equals";

        [Parameter]
        public string LessThanText { get; set; } = "Less than";

        [Parameter]
        public string LessThanOrEqualsText { get; set; } = "Less than or equals";

        [Parameter]
        public string GreaterThanText { get; set; } = "Greater than";

        [Parameter]
        public string GreaterThanOrEqualsText { get; set; } = "Greater than or equals";

        [Parameter]
        public string EndsWithText { get; set; } = "Ends with";

        [Parameter]
        public string ContainsText { get; set; } = "Contains";

        [Parameter]
        public string DoesNotContainText { get; set; } = "Does not contain";

        [Parameter]
        public string StartsWithText { get; set; } = "Starts with";

        internal class NumericFilterEventCallback
        {
            public static EventCallback<T> Create<T>(object receiver, Action<T> action)
            {
                return EventCallback.Factory.Create<T>(receiver, action);
            }

            public static Action<T> Action<T>(Action<object> action)
            {
                return args => action(args);
            }
        }

        [Parameter]
        public FilterCaseSensitivity FilterCaseSensitivity { get; set; } = FilterCaseSensitivity.Default;

        [Parameter]
        public int FilterDelay { get; set; } = 500;

        [Parameter]
        public string FilterDateFormat { get; set; }

        [Parameter]
        public string ColumnWidth { get; set; }

        private string _emptyText = "No records to display.";
        [Parameter]
        public string EmptyText
        {
            get { return _emptyText; }
            set
            {
                if (value != _emptyText)
                {
                    _emptyText = value;
                }
            }
        }

        [Parameter]
        public RenderFragment EmptyTemplate { get; set; }
    #if NET5
        [Parameter]
        public bool AllowVirtualization { get; set; }
    #endif
        [Parameter]
        public bool IsLoading { get; set; }

        [Parameter]
        public bool AllowSorting { get; set; }

        [Parameter]
        public bool AllowMultiColumnSorting { get; set; }

        [Parameter]
        public bool AllowFiltering { get; set; }

        [Parameter]
        public bool AllowColumnResize { get; set; }

        [Parameter]
        public bool AllowColumnReorder { get; set; }

        [Parameter]
        public bool AllowGrouping { get; set; }

        [Parameter]
        public RenderFragment<Group> GroupHeaderTemplate { get; set; }

        [Parameter]
        public string GroupPanelText { get; set; } = "Drag a column header here and drop it to group by that column";

        internal string getColumnResizerId(int columnIndex)
        {
            return string.Join("", $"{UniqueID}".Split('.')) + columnIndex;
        }

        internal async Task StartColumnResize(MouseEventArgs args, int columnIndex)
        {
            await JSRuntime.InvokeVoidAsync("Radzen.startColumnResize", getColumnResizerId(columnIndex), Reference, columnIndex, args.ClientX);
        }

        int? indexOfColumnToReoder;

        internal async Task StartColumnReorder(MouseEventArgs args, int columnIndex)
        {
            indexOfColumnToReoder = columnIndex;
            await JSRuntime.InvokeVoidAsync("Radzen.startColumnReorder", getColumnResizerId(columnIndex));
        }

        internal async Task EndColumnReorder(MouseEventArgs args, int columnIndex)
        {
            if (indexOfColumnToReoder != null)
            {
                var visibleColumns = columns.Where(c => c.Visible).ToList();
                var columnToReorder = visibleColumns.ElementAtOrDefault(indexOfColumnToReoder.Value);
                var columnToReorderTo = visibleColumns.ElementAtOrDefault(columnIndex);

                if (columnToReorder != null && columnToReorderTo != null)
                {
                    var actualColumnIndex = columns.IndexOf(columnToReorderTo);
                    columns.Remove(columnToReorder);
                    columns.Insert(actualColumnIndex, columnToReorder);

                    await ColumnReordered.InvokeAsync(new DataGridColumnReorderedEventArgs<TItem>
                    {
                        Column = columnToReorder,
                        OldIndex = indexOfColumnToReoder.Value,
                        NewIndex = actualColumnIndex
                    });
                }

                indexOfColumnToReoder = null;
            }
        }

        [JSInvokable("RadzenGrid.OnColumnResized")]
        public async Task OnColumnResized(int columnIndex, double value)
        {
            var column = columns.Where(c => c.Visible).ToList()[columnIndex];
            column.SetWidth($"{value}px");
            await ColumnResized.InvokeAsync(new DataGridColumnResizedEventArgs<TItem>
            {
                Column = column,
                Width = value,
            });
        }

        internal string GetOrderBy()
        {
            return string.Join(",", sorts.Select(d => columns.Where(c => c.GetSortProperty() == d.Property).FirstOrDefault()).Where(c => c != null).Select(c => c.GetSortOrderAsString(IsOData())));
        }

        [Parameter]
        public EventCallback<DataGridColumnResizedEventArgs<TItem>> ColumnResized { get; set; }

        [Parameter]
        public EventCallback<DataGridColumnReorderedEventArgs<TItem>> ColumnReordered { get; set; }

        public override IQueryable<TItem> View
        {
            get
            {
                if(LoadData.HasDelegate)
                {
                    return base.View;
                }

                var view = base.View.Where<TItem>(columns);
                var orderBy = GetOrderBy();

                if (!string.IsNullOrEmpty(orderBy))
                {
                    if (typeof(TItem) == typeof(object))
                    {
                        var firstItem = view.FirstOrDefault();
                        if (firstItem != null)
                        {
                            view = view.Cast(firstItem.GetType()).AsQueryable().OrderBy(orderBy).Cast<TItem>();
                        }
                    }
                    else
                    {
                        view = view.OrderBy(orderBy);
                    }
                }

                if (!IsVirtualizationAllowed() || AllowPaging)
                {
                    var count = view.Count();
                    if (count != Count)
                    {
                        Count = count;

                        if (skip >= Count && Count > PageSize)
                        {
                            skip = Count - PageSize;
                        }

                        StateHasChanged();
                    }
                }

                return view;
            }
        }

        internal bool IsVirtualizationAllowed()
        {
    #if NET5
            return AllowVirtualization;
    #else
            return false;
    #endif
        }


        IList<TItem> _value;

        [Parameter]
        public IList<TItem> Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        [Parameter]
        public EventCallback<IList<TItem>> ValueChanged { get; set; }

        [Parameter]
        public EventCallback<TItem> RowSelect { get; set; }

        [Parameter]
        public EventCallback<TItem> RowDeselect { get; set; }

        [Parameter]
        public EventCallback<DataGridRowMouseEventArgs<TItem>> RowClick { get; set; }

        [Parameter]
        public EventCallback<DataGridRowMouseEventArgs<TItem>> RowDoubleClick { get; set; }

        [Parameter]
        public EventCallback<TItem> RowExpand { get; set; }

        [Parameter]
        public EventCallback<TItem> RowCollapse { get; set; }

        [Parameter]
        public Action<RowRenderEventArgs<TItem>> RowRender { get; set; }

        [Parameter]
        public Action<DataGridCellRenderEventArgs<TItem>> CellRender { get; set; }

        [Parameter]
        public Action<DataGridRenderEventArgs<TItem>> Render { get; set; }

        protected override void OnDataChanged()
        {
            Reset(!IsOData() && !LoadData.HasDelegate);
        }

        public void Reset(bool resetColumnState = true, bool resetRowState = false)
        {
            _groupedPagedView = null;
            _view = null;
            _value = new List<TItem>();

            if (resetRowState)
            {
                selectedItems.Clear();
                expandedItems.Clear();
            }

            if (resetColumnState)
            {
                columns.ForEach(c => { c.SetFilterValue(null); c.SetSecondFilterOperator(FilterOperator.Equals); });
                columns.ForEach(c => { c.ResetSortOrder(); });
                sorts.Clear();
           }
        }

        public async override Task Reload()
        {
            _groupedPagedView = null;
            _view = null;

            if (Data != null && !LoadData.HasDelegate)
            {
                Count = 1;
            }
    #if NET5
            if (AllowVirtualization && virtualize != null)
            {
                if(!LoadData.HasDelegate)
                {
                    await virtualize.RefreshDataAsync();
                }
                else
                {
                    Data = null;
                }
            }
    #endif
            var orderBy = GetOrderBy();

            Query.Skip = skip;
            Query.Top = PageSize;
            Query.OrderBy = orderBy;

            var filterString = columns.ToFilterString<TItem>();
            Query.Filter = filterString;

            if (LoadData.HasDelegate)
            {
                await LoadData.InvokeAsync(new Radzen.LoadDataArgs()
                {
                    Skip = skip,
                    Top = PageSize,
                    OrderBy = orderBy,
                    Filter = IsOData() ? columns.ToODataFilterString<TItem>() : filterString,
                    Filters = columns.Where(c => c.Filterable && c.Visible && c.GetFilterValue() != null).Select(c => new FilterDescriptor()
                    {
                        Property = c.GetFilterProperty(),
                        FilterValue = c.GetFilterValue(),
                        FilterOperator = c.GetFilterOperator(),
                        SecondFilterValue = c.GetSecondFilterValue(),
                        SecondFilterOperator = c.GetSecondFilterOperator(),
                        LogicalFilterOperator = c.GetLogicalFilterOperator()
                    }),
                    Sorts = sorts
                }); ;
            }

            CalculatePager();

            if (!LoadData.HasDelegate)
            {
                StateHasChanged();
            }
            else
            {
    #if NET5
                if (AllowVirtualization && virtualize != null)
                {
                    await virtualize.RefreshDataAsync();
                }
    #endif        
            } 
       }

        internal async Task ChangeState()
        {
            await InvokeAsync(StateHasChanged);
        }

        protected override Task OnParametersSetAsync()
        {
            if (Visible && !LoadData.HasDelegate && _view == null)
            {
                InvokeAsync(Reload);
            }
            else
            {
                CalculatePager();
            }

            return Task.CompletedTask;
        }

        internal Dictionary<RadzenDataGridGroupRow<TItem>, bool> collapsedGroupItems = new Dictionary<RadzenDataGridGroupRow<TItem>, bool>();
        internal string ExpandedGroupItemStyle(RadzenDataGridGroupRow<TItem> item)
        {
            return collapsedGroupItems.Keys.Contains(item) ? "rz-row-toggler rzi-grid-sort  rzi-chevron-circle-right" : "rz-row-toggler rzi-grid-sort  rzi-chevron-circle-down";
        }

        internal bool IsGroupItemExpanded(RadzenDataGridGroupRow<TItem> item)
        {
            return !collapsedGroupItems.Keys.Contains(item) ;
        }
    
        internal async System.Threading.Tasks.Task ExpandGroupItem(RadzenDataGridGroupRow<TItem> item)
        {
            if (!collapsedGroupItems.Keys.Contains(item))
            {
                collapsedGroupItems.Add(item, true);
            }
            else
            {
                collapsedGroupItems.Remove(item);
            }

            await InvokeAsync(StateHasChanged);
        }

        internal Dictionary<TItem, bool> expandedItems = new Dictionary<TItem, bool>();
        internal string ExpandedItemStyle(TItem item)
        {
            return expandedItems.Keys.Contains(item) ? "rz-row-toggler rzi-grid-sort  rzi-chevron-circle-down" : "rz-row-toggler rzi-grid-sort  rzi-chevron-circle-right";
        }

        internal Dictionary<TItem, bool> selectedItems = new Dictionary<TItem, bool>();
        internal string RowStyle(TItem item, int index)
        {
            var evenOrOdd = index % 2 == 0 ? "rz-datatable-even" : "rz-datatable-odd";

            return (RowSelect.HasDelegate || ValueChanged.HasDelegate || SelectionMode == DataGridSelectionMode.Multiple) && selectedItems.Keys.Contains(item) ? $"rz-state-highlight {evenOrOdd} " : $"{evenOrOdd} ";
        }

        internal Tuple<Radzen.RowRenderEventArgs<TItem>, IReadOnlyDictionary<string, object>> RowAttributes(TItem item)
        {
            var args = new Radzen.RowRenderEventArgs<TItem>() { Data = item, Expandable = Template != null };

            if (RowRender != null)
            {
                RowRender(args);
            }

            return new Tuple<Radzen.RowRenderEventArgs<TItem>, IReadOnlyDictionary<string, object>>(args, new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(args.Attributes));
        }

        private bool visibleChanged = false;
        private bool firstRender = true;
        public override async Task SetParametersAsync(ParameterView parameters)
        {
            var emptyTextChanged = parameters.DidParameterChange(nameof(EmptyText), EmptyText);
            if (emptyTextChanged)
            {
                await ChangeState();
            }

            visibleChanged = parameters.DidParameterChange(nameof(Visible), Visible);

            bool valueChanged = parameters.DidParameterChange(nameof(Value), Value);

            await base.SetParametersAsync(parameters);

            if (valueChanged)
            {
                selectedItems.Clear();

                if (Value != null)
                {
                    Value.Where(v => v != null).ToList().ForEach(v => selectedItems.Add(v, true));
                }
            }

            if (visibleChanged && !firstRender)
            {
                if (Visible == false)
                {
                    Dispose();
                }
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                StateHasChanged();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (Visible)
            {
                var args = new Radzen.DataGridRenderEventArgs<TItem>() { Grid = this, FirstRender = firstRender };

                if (Render != null)
                {
                    Render(args);
                }
            }

            this.firstRender = firstRender;

            if (firstRender || visibleChanged)
            {
                visibleChanged = false;
            }
        }

        public async System.Threading.Tasks.Task ExpandRow(TItem item)
        {
            await ExpandItem(item);
        }

        internal async System.Threading.Tasks.Task ExpandItem(TItem item)
        {
            if (ExpandMode == DataGridExpandMode.Single && expandedItems.Keys.Any())
            {
                var itemToCollapse = expandedItems.Keys.FirstOrDefault();
                if (itemToCollapse != null)
                {
                    expandedItems.Remove(itemToCollapse);
                    await RowCollapse.InvokeAsync(itemToCollapse);

                    if (object.Equals(item, itemToCollapse))
                    {
                        return;
                    }

                }
            }

            if (!expandedItems.Keys.Contains(item))
            {
                expandedItems.Add(item, true);
                await RowExpand.InvokeAsync(item);
            }
            else
            {
                expandedItems.Remove(item);
                await RowCollapse.InvokeAsync(item);
            }

            await InvokeAsync(StateHasChanged);
        }

        [Parameter]
        public DataGridSelectionMode SelectionMode { get; set; } = DataGridSelectionMode.Single;

        internal async Task OnRowClick(DataGridRowMouseEventArgs<TItem> args)
        {
            await RowClick.InvokeAsync(args);
            await OnRowSelect(args.Data);
        }

        internal async System.Threading.Tasks.Task OnRowSelect(object item, bool raiseChange = true)
        {
            if (SelectionMode == DataGridSelectionMode.Single && item != null && selectedItems.Keys.Contains((TItem)item))
            {
                // Legacy RowSelect raise
                if (raiseChange)
                {
                    await RowSelect.InvokeAsync((TItem)item);
                }
                return;
            }

            if (SelectionMode == DataGridSelectionMode.Single && selectedItems.Keys.Any())
            {
                var itemToDeselect = selectedItems.Keys.FirstOrDefault();
                if (itemToDeselect != null)
                {
                    selectedItems.Remove(itemToDeselect);
                    await RowDeselect.InvokeAsync(itemToDeselect);
                }
            }

            if (item != null)
            {
                if (!selectedItems.Keys.Contains((TItem)item))
                {
                    selectedItems.Add((TItem)item, true);
                    if (raiseChange)
                    {
                        await RowSelect.InvokeAsync((TItem)item);
                    }
                }
                else
                {
                    selectedItems.Remove((TItem)item);
                    await RowDeselect.InvokeAsync((TItem)item);
                }
            }
            else
            {
                if (raiseChange)
                {
                    await RowSelect.InvokeAsync((TItem)item);
                }
            }

            var value = selectedItems.Keys;

            _value = SelectionMode == DataGridSelectionMode.Multiple ? new List<TItem>(value) : new List<TItem>() { value.FirstOrDefault() };

            await ValueChanged.InvokeAsync(_value);

            StateHasChanged();
        }

        public async System.Threading.Tasks.Task SelectRow(TItem item)
        {
            await OnRowSelect(item, true);
        }

        internal async System.Threading.Tasks.Task OnRowDblClick(DataGridRowMouseEventArgs<TItem> args)
        {
            await RowDoubleClick.InvokeAsync(args);
        }

        [Parameter]
        public EventCallback<TItem> RowEdit { get; set; }

        [Parameter]
        public EventCallback<TItem> RowUpdate { get; set; }

        [Parameter]
        public EventCallback<TItem> RowCreate { get; set; }

        internal Dictionary<TItem, bool> editedItems = new Dictionary<TItem, bool>();
        internal Dictionary<TItem, EditContext> editContexts = new Dictionary<TItem, EditContext>();

        public async System.Threading.Tasks.Task EditRow(TItem item)
        {
            if(itemToInsert != null)
            {
                CancelEditRow(itemToInsert);
            }

            await EditRowInternal(item);
        }

        async System.Threading.Tasks.Task EditRowInternal(TItem item)
        {
            if (EditMode == DataGridEditMode.Single && editedItems.Keys.Any())
            {
                var itemToCancel = editedItems.Keys.FirstOrDefault();
                if (itemToCancel != null)
                {
                    editedItems.Remove(itemToCancel);
                    editContexts.Remove(itemToCancel);
                }
            }

            if (!editedItems.Keys.Contains(item))
            {
                editedItems.Add(item, true);

                var editContext = new EditContext(item);
                editContexts.Add(item, editContext);

                await RowEdit.InvokeAsync(item);

                StateHasChanged();
            }
        }

        public async System.Threading.Tasks.Task UpdateRow(TItem item)
        {
            if (editedItems.Keys.Contains(item))
            {
                var editContext = editContexts[item];

                if (editContext.Validate())
                {
                    editedItems.Remove(item);
                    editContexts.Remove(item);

                    if (object.Equals(itemToInsert, item))
                    {
                        await RowCreate.InvokeAsync(item);
                        itemToInsert = default(TItem);
                    }
                    else
                    {
                        await RowUpdate.InvokeAsync(item);
                    }
                }

                StateHasChanged();
            }
        }

        public void CancelEditRow(TItem item)
        {
            if (object.Equals(itemToInsert, item))
            {
                if(!IsVirtualizationAllowed())
                {
                    var list = this.PagedView.ToList();
                    list.Remove(item);
                    this._view = list.AsQueryable();
                    this.Count--;
                    itemToInsert = default(TItem);
                    StateHasChanged();
                }
                else
                {
        #if NET5
                    itemToInsert = default(TItem);
                    if (virtualize != null)
                    {
                        virtualize.RefreshDataAsync();
                    }
        #endif
                }
            }
            else
            {
                int hash = item.GetHashCode();

                if (editedItems.Keys.Contains(item))
                {
                    editedItems.Remove(item);
                    editContexts.Remove(item);

                    StateHasChanged();
                }
            }
        }

        public bool IsRowInEditMode(TItem item)
        {
            return editedItems.Keys.Contains(item);
        }

        TItem itemToInsert;
        public async System.Threading.Tasks.Task InsertRow(TItem item)
        {
            itemToInsert = item;
            if(!IsVirtualizationAllowed())
            {
                var list = this.PagedView.ToList();
                list.Insert(0, item);
                this._view = list.AsQueryable();
                this.Count++;
            }
            else
            {
    #if NET5
                if (virtualize != null)
                {
                    await virtualize.RefreshDataAsync();
                }
    #endif
            }
            
           await EditRowInternal(item);
        }

        bool? isOData;
        internal bool IsOData()
        {
            if(isOData == null && Data != null)
            {
                isOData = typeof(ODataEnumerable<TItem>).IsAssignableFrom(Data.GetType());
            }

            return isOData != null ? isOData.Value : false;
        }

        internal List<SortDescriptor> sorts = new List<SortDescriptor>();
        internal void SetColumnSortOrder(RadzenDataGridColumn<TItem> column)
        {
            if (!AllowMultiColumnSorting)
            {
                foreach (var c in columns.Where(c => c != column))
                {
                    c.SetSortOrder(null);
                }
                sorts.Clear();
            }

            var descriptor = sorts.Where(d => d.Property == column?.GetSortProperty()).FirstOrDefault();
            if (descriptor == null)
            {
                descriptor = new SortDescriptor() { Property = column.GetSortProperty() };
            }

            if (column.GetSortOrder() == null)
            {
                column.SetSortOrder(SortOrder.Ascending);
                descriptor.SortOrder = SortOrder.Ascending;
            }
            else if (column.GetSortOrder() == SortOrder.Ascending)
            {
                column.SetSortOrder(SortOrder.Descending);
                descriptor.SortOrder = SortOrder.Descending;
            }
            else if (column.GetSortOrder() == SortOrder.Descending)
            {
                column.SetSortOrder(null);
                if (sorts.Where(d => d.Property == column?.GetSortProperty()).Any())
                {
                    sorts.Remove(descriptor);
                }
                descriptor = null;
            }

            if (descriptor != null && !sorts.Where(d => d.Property == column?.GetSortProperty()).Any())
            {
                sorts.Add(descriptor);
            }
        }

        public List<GroupDescriptor> Groups 
        { 
            get
            {
                return groups;
            }
            set
            {
                groups = value;
            }
 
         }

        internal List<GroupDescriptor> groups = new List<GroupDescriptor>();
        internal void EndColumnDropToGroup()
        {
            if(indexOfColumnToReoder != null)
            {
                var column = columns.Where(c => c.Visible).ElementAtOrDefault(indexOfColumnToReoder.Value);

                if(column != null && column.Groupable && !string.IsNullOrEmpty(column.GetGroupProperty()))
                {
                    var descriptor = groups.Where(d => d.Property == column.GetGroupProperty()).FirstOrDefault();
                    if (descriptor == null)
                    {
                        descriptor = new GroupDescriptor() { Property = column.GetGroupProperty(), Title = column.Title };
                        groups.Add(descriptor);
                        _groupedPagedView = null;
                    }
                }

                indexOfColumnToReoder = null;
            }  
        }

        public void OrderBy(string property)
        {
            var p = IsOData() ? property.Replace('.', '/') : PropertyAccess.GetProperty(property);

            var column = columns.Where(c => c.GetSortProperty() == property).FirstOrDefault();
            if (column != null)
            {
                SetColumnSortOrder(column);
            }

            if (LoadData.HasDelegate && IsVirtualizationAllowed())
            {
                Data = null;
            }

            InvokeAsync(Reload);
        }

        public void OrderByDescending(string property)
        {
            var column = columns.Where(c => c.GetSortProperty() == property).FirstOrDefault();
            if (column != null)
            {
                column.SetSortOrder(SortOrder.Descending);
            }
            InvokeAsync(Reload);
        }

        protected override string GetComponentCssClass()
        {
            var additionalClasses = new List<string>();

            if (CurrentStyle.ContainsKey("height"))
            {
                additionalClasses.Add("rz-has-height");
            }

            if (RowSelect.HasDelegate || ValueChanged.HasDelegate || SelectionMode == DataGridSelectionMode.Multiple)
            {
                additionalClasses.Add("rz-selectable");
            }

            return $"rz-has-paginator rz-datatable  rz-datatable-scrollable {String.Join(" ", additionalClasses)}";
        }

        internal string getHeaderStyle()
        {
            var additionalStyle = Style != null && Style.IndexOf("height:") != -1 ? "padding-right: 17px;" : "";
            return $"margin-left:0px;{additionalStyle}";
        }

        public Query Query { get; private set; } = new Query();

        internal string PopupID
        {
            get
            {
                return $"popup{UniqueID}";
            }
        }

        internal bool disposed = false;

        public override void Dispose()
        {
            base.Dispose();

            disposed = true;

            if (IsJSRuntimeAvailable)
            {
                foreach (var column in columns.Where(c => c.Visible))
                {
                    JSRuntime.InvokeVoidAsync("Radzen.destroyPopup", $"{PopupID}{column.GetFilterProperty()}");
                }
            }
        }
    }
}