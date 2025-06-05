// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Sarif.Viewer.FileMonitor
{
    public interface IQLFileMonitor
    {
        void StartWatch(string solutionFolder = null);

        void StopWatch();
    }
}
