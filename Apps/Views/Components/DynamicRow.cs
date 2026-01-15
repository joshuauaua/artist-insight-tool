namespace ArtistInsightTool.Apps.Views;

public class DynamicRow
{
  private readonly Dictionary<int, object?> _values = new();
  public void SetValue(int index, object? value) => _values[index] = value;
  public object? GetValue(int index) => _values.TryGetValue(index, out var v) ? v : null;

  public object? Col0 => GetValue(0); public object? Col1 => GetValue(1); public object? Col2 => GetValue(2);
  public object? Col3 => GetValue(3); public object? Col4 => GetValue(4); public object? Col5 => GetValue(5);
  public object? Col6 => GetValue(6); public object? Col7 => GetValue(7); public object? Col8 => GetValue(8);
  public object? Col9 => GetValue(9); public object? Col10 => GetValue(10);
  public object? Col11 => GetValue(11); public object? Col12 => GetValue(12); public object? Col13 => GetValue(13);
  public object? Col14 => GetValue(14); public object? Col15 => GetValue(15); public object? Col16 => GetValue(16);
  public object? Col17 => GetValue(17); public object? Col18 => GetValue(18); public object? Col19 => GetValue(19);
  public object? Col20 => GetValue(20); public object? Col21 => GetValue(21); public object? Col22 => GetValue(22);
  public object? Col23 => GetValue(23); public object? Col24 => GetValue(24); public object? Col25 => GetValue(25);
  public object? Col26 => GetValue(26); public object? Col27 => GetValue(27); public object? Col28 => GetValue(28);
  public object? Col29 => GetValue(29); public object? Col30 => GetValue(30); public object? Col31 => GetValue(31);
  public object? Col32 => GetValue(32); public object? Col33 => GetValue(33); public object? Col34 => GetValue(34);
  public object? Col35 => GetValue(35); public object? Col36 => GetValue(36); public object? Col37 => GetValue(37);
  public object? Col38 => GetValue(38); public object? Col39 => GetValue(39); public object? Col40 => GetValue(40);
  public object? Col41 => GetValue(41); public object? Col42 => GetValue(42); public object? Col43 => GetValue(43);
  public object? Col44 => GetValue(44); public object? Col45 => GetValue(45); public object? Col46 => GetValue(46);
  public object? Col47 => GetValue(47); public object? Col48 => GetValue(48); public object? Col49 => GetValue(49);
}
