// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// MUX Reference ListPickerFlyoutPresenterAutomationPeer_Partial.cpp, tag winui3/release/1.4.2
namespace Microsoft.UI.Xaml.Automation.Peers;

/// <summary>
/// Exposes ListPickerFlyoutPresenter types to Microsoft UI Automation.
/// </summary>
public partial class ListPickerFlyoutPresenterAutomationPeer : FrameworkElementAutomationPeer
{
	internal ListPickerFlyoutPresenterAutomationPeer()
	{

	}

	protected override string GetClassNameCore() => nameof(Controls.ListPickerFlyoutPresenter);

	protected override AutomationControlType GetAutomationControlTypeCore()
		=> AutomationControlType.Pane;

	protected override string GetNameCore() 
	{
		//UNO TODO: FindStringResource
		//IFC(Private::FindStringResource(
		//UIA_AP_LISTPICKERFLYOUT_NAME,
		//returnValue));
		return base.GetNameCore();
	}
}
