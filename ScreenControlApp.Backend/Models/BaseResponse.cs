namespace ScreenControlApp.Backend.Models {
	public class BaseResponse {		
		public bool IsSuccess { get; set; } = default!;
		public string Message { get; set; } = default!;
		public BaseResponse() => IsSuccess = true;
		//public BaseResponse(string message, bool success) {
		//	IsSuccess = success;
		//	Message = message;
		//}
	}
}
