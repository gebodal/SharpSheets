using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.Dialogues {

	public class MsBoxSharpViewModel : MsBoxStandardViewModel {

		public Icon Icon { get; private set; }

		public MsBoxSharpViewModel(MessageBoxStandardParams msBoxParams) : base(msBoxParams) {
			SetIcon(msBoxParams.Icon);
		}

		public bool IsInformationIcon { get; private set; } = false;
		public bool IsQuestionIcon { get; private set; } = false;
		public bool IsErrorIcon { get; private set; } = false;
		public bool IsWarningIcon { get; private set; } = false;
		public bool IsOtherIcon { get; private set; } = false;

		private void SetIcon(Icon icon) {
			switch (icon) {
				case Icon.None:
					break;
				case Icon.Info:
					IsInformationIcon = true;
					break;
				case Icon.Question:
					IsQuestionIcon = true;
					break;
				case Icon.Error:
					IsErrorIcon = true;
					break;
				case Icon.Warning:
					IsWarningIcon = true;
					break;
				default:
					IsOtherIcon = true;
					break;
			}
		}

	}

}
