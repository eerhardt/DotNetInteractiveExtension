// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace XPlot.Plotly
{
    public static class XPlotExtensions
    {
        public static Action<PlotlyChart> OnShow;

        public static void Display(this PlotlyChart chart)
        {
            Action<PlotlyChart> onShow = OnShow;
            if (onShow != null)
            {
                onShow(chart);
            }
            else
            {
                chart.Show();
            }
        }
    }
}
