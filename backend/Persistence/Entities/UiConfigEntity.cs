namespace TubeArr.Backend.Data;

public sealed class UiConfigEntity
{
	public int Id { get; set; } = 1;

	public int FirstDayOfWeek { get; set; } = 0;
	public string CalendarWeekColumnHeader { get; set; } = "ddd M/D";
	public string ShortDateFormat { get; set; } = "MMM D YYYY";
	public string LongDateFormat { get; set; } = "dddd, MMMM D YYYY";
	public string TimeFormat { get; set; } = "h(:mm)a";
	public bool ShowRelativeDates { get; set; } = true;
	public string Theme { get; set; } = "dark";
	public bool EnableColorImpairedMode { get; set; } = false;
	public int UiLanguage { get; set; } = 0;
}

