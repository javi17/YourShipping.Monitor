﻿namespace YourShipping.WishList.Pages
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using Blorc.Components;
    using Blorc.PatternFly.Components.Table;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    using YourShipping.Monitor.Shared;

    public class DepartmentsComponent : BlorcComponentBase
    {
        public bool IsLoading
        {
            get => this.GetPropertyValue<bool>(nameof(this.IsLoading));
            set => this.SetPropertyValue(nameof(this.IsLoading), value);
        }

        public string Url
        {
            get => this.GetPropertyValue<string>(nameof(this.Url));
            set => this.SetPropertyValue(nameof(this.Url), value);
        }

        protected List<Department> Departments
        {
            get => this.GetPropertyValue<List<Department>>(nameof(this.Departments));
            set => this.SetPropertyValue(nameof(this.Departments), value);
        }

        [Inject]
        protected HttpClient HttpClient { get; set; }

        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        public IEnumerable<ActionDefinition> GetActions(object row)
        {
            var actionDefinitions = new List<ActionDefinition>();
            if (row is Department department)
            {
                actionDefinitions.Add(
                    new CallActionDefinition
                    {
                        Label = "Open",
                        IsDisabled = department.ProductsCount == 0,
                        Action = async o => await this.Open(o as Department)
                    });
                actionDefinitions.Add(
                    new CallActionDefinition
                    {
                        Label = "Delete",
                        Action = async o => await this.Delete(o as Department)
                    });
                actionDefinitions.Add(
                    new SeparatorActionDefinition());
                actionDefinitions.Add(
                    new CallActionDefinition
                    {
                        Label = "Add all products",
                        IsDisabled = department.ProductsCount == 0,
                        Action = async o => await this.AddAll(o as Department)
                    });

                return actionDefinitions;
            }

            return actionDefinitions;
        }

        protected async Task AddAsync()
        {
            await this.HttpClient.PostAsync("Departments", JsonContent.Create(new Uri(this.Url)));
            this.Url = string.Empty;
            await this.RefreshAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading)
            {
                this.Departments = (await this.HttpClient.GetFromJsonAsync<Department[]>("Departments")).ToList();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            await this.RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Departments) && this.Departments != null)
            {
                this.IsLoading = false;
            }
        }

        protected async Task RefreshAsync()
        {
            this.Departments = null;
            this.IsLoading = true;
        }

        private async Task AddAll(Department department)
        {
        }

        private async Task Delete(Department department)
        {
            await this.HttpClient.DeleteAsync($"Departments/{department.Id}");
            await this.RefreshAsync();
        }

        private async Task Open(Department department)
        {
            if (department != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", department.Url, "_blank");
            }
        }
    }
}