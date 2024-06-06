using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia.Windows {

	public enum MessageBoxButton {
		OK,
		YesNo,
		OkCancel,
		OkAbort,
		YesNoCancel,
		YesNoAbort
	}

	public enum MessageBoxResult {
		OK,
		Yes,
		No,
		Abort,
		Cancel,
		None
	}

	public enum MessageBoxImage {
		None,
		Error,
		Folder,
		Forbidden,
		Information,
		Question,
		Setting,
		Stop,
		Success,
		Warning
	}

	internal static class MessageBoxEnumUtils {

		public static ButtonEnum AsButtonEnum(this MessageBoxButton button) {
			return button switch {
				MessageBoxButton.OK => ButtonEnum.Ok,
				MessageBoxButton.YesNo => ButtonEnum.YesNo,
				MessageBoxButton.OkCancel => ButtonEnum.OkCancel,
				MessageBoxButton.OkAbort => ButtonEnum.OkAbort,
				MessageBoxButton.YesNoCancel => ButtonEnum.YesNoCancel,
				MessageBoxButton.YesNoAbort => ButtonEnum.YesNoAbort,
				_ => throw new InvalidOperationException($"Invalid {nameof(MessageBoxButton)} value.")
			};
		}

		public static MessageBoxResult AsMessageBoxResult(this ButtonResult result) {
			return result switch {
				ButtonResult.Ok => MessageBoxResult.OK,
				ButtonResult.Yes => MessageBoxResult.Yes,
				ButtonResult.No => MessageBoxResult.No,
				ButtonResult.Abort => MessageBoxResult.Abort,
				ButtonResult.Cancel => MessageBoxResult.Cancel,
				ButtonResult.None => MessageBoxResult.None,
				_ => throw new InvalidOperationException($"Invalid {nameof(ButtonResult)} value.")
			};
		}

		public static Icon AsIcon(this MessageBoxImage image) {
			return image switch {
				MessageBoxImage.None => Icon.None,
				MessageBoxImage.Error => Icon.Error,
				MessageBoxImage.Folder => Icon.Folder,
				MessageBoxImage.Forbidden => Icon.Forbidden,
				MessageBoxImage.Information => Icon.Info,
				MessageBoxImage.Question => Icon.Question,
				MessageBoxImage.Setting => Icon.Setting,
				MessageBoxImage.Stop => Icon.Stop,
				MessageBoxImage.Success => Icon.Success,
				MessageBoxImage.Warning => Icon.Warning,
				_ => throw new InvalidOperationException($"Invalid {nameof(MessageBoxButton)} value.")
			};
		}

	}

	public static class MessageBoxes {

		public static async Task<MessageBoxResult> Show(string text, string title, MessageBoxButton button, MessageBoxImage image, WindowStartupLocation startupLocation = WindowStartupLocation.CenterOwner) {
			IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(title, text, button.AsButtonEnum(), image.AsIcon(), startupLocation);

			ButtonResult result = await box.ShowAsync();

			return result.AsMessageBoxResult();
		}

	}

}
