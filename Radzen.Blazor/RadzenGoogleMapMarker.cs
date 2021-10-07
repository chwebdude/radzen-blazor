﻿using Microsoft.AspNetCore.Components;

namespace Radzen.Blazor
{
    public class RadzenGoogleMapMarker : RadzenComponent
    {
        [Parameter]
        public GoogleMapPosition Position { get; set; } = new GoogleMapPosition() { Lat = 0, Lng = 0 };

        [Parameter]
        public string Title { get; set; }

        [Parameter]
        public string Label { get; set; }

        RadzenGoogleMap _map;

        [CascadingParameter]
        public RadzenGoogleMap Map
        {
            get
            {
                return _map;
            }
            set
            {
                if (_map != value)
                {
                    _map = value;
                    _map.AddMarker(this);
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            Map?.RemoveMarker(this);
        }
    }
}