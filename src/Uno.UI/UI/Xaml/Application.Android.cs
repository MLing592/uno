﻿using System;
using Android.Content.Res;
using Android.OS;
using Uno.UI.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Globalization;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.UI.ViewManagement;
using Colors = Microsoft.UI.Colors;
using Uno.UI.Xaml.Controls;

namespace Microsoft.UI.Xaml;

public partial class Application
{
	partial void InitializePartial()
	{
		PermissionsHelper.Initialize();
	}

	static partial void StartPartial(ApplicationInitializationCallback callback)
	{
		callback(new ApplicationInitializationCallbackParams());
	}

	/// <remarks>
	/// The 5 second timeout seems to be the safest timeout for suspension activities.
	/// See - https://stackoverflow.com/a/3987733/732221
	/// </remarks>
	private DateTimeOffset GetSuspendingOffset() => DateTimeOffset.Now.AddSeconds(5);

	partial void ApplySystemOverlaysTheming()
	{
		// This is needed only due to the fact that currently Instance accessor creates the wrapper
		// eagerly - which could then happen too early. Will no longer be needed when un-singletoned.
		if (InitializationComplete)
		{
			NativeWindowWrapper.Instance.ApplySystemOverlaysTheming();
		}
	}
}
