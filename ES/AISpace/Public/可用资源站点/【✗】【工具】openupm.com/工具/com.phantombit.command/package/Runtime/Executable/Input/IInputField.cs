using UnityEngine;
using WellFired.Command.Skins;

namespace WellFired.Command.Unity.Runtime.Input
{
	public interface IInputField 
	{
		bool HasFocus 						{ get; }
		Rect Rect 							{ get; }
		string PreviousCompleteInput 		{ get; }
		string[] PreviousCompleteParameters { get; }
		int CurrentParameterIndex			{ get; }

		string Input						{ get; set; }

		void FinaliseInput();
		void Focus();
		void LoseFocus();
		void Draw(ISkinData skinData);
	}
}