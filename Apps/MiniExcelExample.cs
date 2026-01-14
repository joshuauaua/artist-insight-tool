using Ivy.Shared;
using MiniExcelLibs;
using System.IO;

namespace MiniExcelExample;

// --- Domain Models ---

public class Student
{
  public Guid ID { get; set; }
  public string Name { get; set; } = "";
  public string Email { get; set; } = "";
  public string Course { get; set; } = "";
  public string Grade { get; set; } = "";
  public int Age { get; set; }
  public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

// --- Services ---

public static class StudentService
{
  private static List<Student> _students = new()
    {
        new Student { ID = Guid.NewGuid(), Name = "Alice Johnson", Email = "alice@example.com", Course = "Computer Science", Grade = "A", Age = 20 },
        new Student { ID = Guid.NewGuid(), Name = "Bob Smith", Email = "bob@example.com", Course = "Mathematics", Grade = "B+", Age = 22 },
        new Student { ID = Guid.NewGuid(), Name = "Charlie Brown", Email = "charlie@example.com", Course = "Physics", Grade = "A-", Age = 21 }
    };

  public static event Action? DataChanged;

  public static List<Student> GetStudents()
  {
    return _students.ToList();
  }

  public static void AddStudent(Student student)
  {
    if (student.ID == Guid.Empty) student.ID = Guid.NewGuid();
    _students.Add(student);
    DataChanged?.Invoke();
  }

  public static void UpdateStudents(List<Student> students)
  {
    _students = students;
    DataChanged?.Invoke();
  }

  public static void UpdateStudent(Student student)
  {
    var existing = _students.FirstOrDefault(s => s.ID == student.ID);
    if (existing != null)
    {
      existing.Name = student.Name;
      existing.Email = student.Email;
      existing.Course = student.Course;
      existing.Grade = student.Grade;
      existing.Age = student.Age;
      DataChanged?.Invoke();
    }
  }

  public static void DeleteStudent(Guid id)
  {
    var item = _students.FirstOrDefault(s => s.ID == id);
    if (item != null)
    {
      _students.Remove(item);
      DataChanged?.Invoke();
    }
  }
}

// --- Helpers ---

public record ListItem(string Title, string Subtitle, Action<Event<ListItem>> OnClick, object Tag);

// Renamed to avoid conflicts
public class EffectDisposable(Action action) : IDisposable
{
  public void Dispose() => action();
}

public static class BladeHelper
{
  public static object WithHeader(object header, object content)
  {
    return Layout.Vertical().Gap(20).Padding(20)
        .Add(header)
        .Add(new Separator())
        .Add(content);
  }
}


// --- Apps ---

[App(icon: Icons.Sheet, title: "MiniExcel - Edit", path: ["Examples", "MiniExcel - Edit"])]
public class MiniExcelEditApp : ViewBase
{
  public override object? Build()
  {
    return this.UseBlades(() => new StudentsListBlade(), "Students");
  }
}

public class StudentsListBlade : ViewBase
{
  public override object? Build()
  {
    var blades = this.UseContext<IBladeController>();
    var refreshToken = this.UseRefreshToken();
    var searchTerm = this.UseState("");
    var students = this.UseState(() => StudentService.GetStudents());

    this.UseEffect(async () =>
    {
      students.Set(StudentService.GetStudents());
      return (IDisposable?)null;
    }, [refreshToken.ToTrigger()]);

    this.UseEffect(async () =>
    {
      void OnDataChanged()
      {
        students.Set(StudentService.GetStudents());
        refreshToken.Refresh();
      }
      StudentService.DataChanged += OnDataChanged;
      return (IDisposable?)new EffectDisposable(() => StudentService.DataChanged -= OnDataChanged);
    }, []);


    var filteredStudents = string.IsNullOrWhiteSpace(searchTerm.Value)
        ? students.Value
        : students.Value.Where(s =>
            s.Name.Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase) ||
            s.Email.Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase) ||
            s.Course.Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase) ||
            s.Grade.ToString().Contains(searchTerm.Value, StringComparison.OrdinalIgnoreCase) ||
            s.Age.ToString().Contains(searchTerm.Value)
        ).ToList();

    var addButton = Icons.Plus
        .ToButton()
        .Variant(ButtonVariant.Primary)
        .ToTrigger((isOpen) => new StudentCreateDialog(isOpen, refreshToken, students));

    // Use a container for the list content
    var content = Layout.Vertical().Gap(5);

    if (filteredStudents.Count > 0)
    {
      foreach (var s in filteredStudents)
      {
        // Explicitly wrapping in Card, relying on GlobalUsings for Card
        var card = new Card(
            Layout.Vertical().Padding(10)
            .Add(Text.Markdown($"**{s.Name}**"))
            .Add(Text.Small($"{s.Course} • Grade: {s.Grade}"))
        ).HandleClick(e => blades.Push(this, new StudentDetailBlade(s.ID, () => refreshToken.Refresh()), s.Name));

        content.Add(card);
      }
    }
    else if (students.Value.Count > 0)
    {
      content.Add(Layout.Center().Add(Text.Small($"No students found matching '{searchTerm.Value}'")));
    }
    else
    {
      content.Add(Layout.Center().Add(Text.Small("No students. Add the first record.")));
    }

    return BladeHelper.WithHeader(
        Layout.Horizontal().Gap(10)
            .Add(searchTerm.ToTextInput().Placeholder("Search students...").Width(Size.Grow()))
            .Add(addButton)
        ,
        content
    );
  }
}

public class StudentDetailBlade(Guid studentId, Action? onRefresh = null) : ViewBase
{
  public override object? Build()
  {
    var blades = this.UseContext<IBladeController>();
    var refreshToken = this.UseRefreshToken();
    var (alertView, showAlert) = this.UseAlert();

    var initialStudent = StudentService.GetStudents().FirstOrDefault(s => s.ID == studentId);

    if (initialStudent == null)
    {
      return null;
    }

    var student = this.UseState(initialStudent);

    Student? GetCurrentStudent() => StudentService.GetStudents().FirstOrDefault(s => s.ID == studentId);

    void RefreshStudentData()
    {
      var updatedStudent = GetCurrentStudent();
      if (updatedStudent != null)
      {
        student.Set(updatedStudent);
      }
    }

    this.UseEffect(async () =>
    {
      RefreshStudentData();
      return (IDisposable?)null;
    }, [refreshToken.ToTrigger()]);

    var studentValue = student.Value;

    var editButton = new Button("Edit")
        .Icon(Icons.Pencil)
        .Variant(ButtonVariant.Outline)
        .ToTrigger((isOpen) => new StudentEditSheet(isOpen, studentId, refreshToken, () =>
        {
          RefreshStudentData();
          onRefresh?.Invoke();
        }));

    var onDelete = new Action(() =>
    {
      showAlert($"Are you sure you want to delete {studentValue.Name}?", result =>
          {
          if (result.IsOk())
          {
            StudentService.DeleteStudent(studentId);
            refreshToken.Refresh();
            onRefresh?.Invoke();
            blades.Pop(refresh: true);
          }
        }, "Delete Student", AlertButtonSet.OkCancel);
    });

    return new Fragment(
        BladeHelper.WithHeader(
            Text.H4(studentValue.Name)
            ,
            Layout.Vertical().Gap(10)
                .Add(new Card(
                    Layout.Vertical().Gap(10)
                    .Add(new
                    {
                      Email = studentValue.Email,
                      Age = studentValue.Age,
                      Course = studentValue.Course,
                      Grade = studentValue.Grade
                    }.ToDetails())
                    .Add(Layout.Horizontal().Gap(5)
                        .Add(editButton)
                        .Add(new Button("Delete")
                            .Icon(Icons.Trash)
                            .Variant(ButtonVariant.Destructive)
                            .HandleClick(_ => onDelete())
                        )
                    )
                ))
        ),
        alertView
    );
  }
}


public class StudentCreateDialog(IState<bool> isOpen, RefreshToken refreshToken, IState<List<Student>> students) : ViewBase
{
  public override object? Build()
  {
    var name = UseState("");
    var email = UseState("");
    var course = UseState("");
    var grade = UseState("");
    var age = UseState(18);
    var ageStr = UseState("18");

    var onSave = new Action(() =>
    {
      var newStudent = new Student
      {
        Name = name.Value,
        Email = email.Value,
        Course = course.Value,
        Grade = grade.Value,
        Age = age.Value
      };
      StudentService.AddStudent(newStudent);
      students.Set(StudentService.GetStudents());
      refreshToken.Refresh();
      isOpen.Set(false);
    });

    // Sync age string -> int
    this.UseEffect(async () =>
    {
      if (int.TryParse(ageStr.Value, out var val)) age.Set(val);
      return (IDisposable?)null;
    }, [ageStr]);

    return Layout.Vertical().Gap(15).Width(400)
            .Add(Text.H3("Add Student"))
            .Add(Layout.Vertical().Gap(5).Add("Name").Add(name.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Email").Add(email.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Course").Add(course.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Grade").Add(grade.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Age").Add(ageStr.ToTextInput()))
            .Add(Layout.Horizontal().Gap(10).Align(Align.Right)
                .Add(new Button("Cancel").Variant(ButtonVariant.Outline).HandleClick(_ => isOpen.Set(false)))
                .Add(new Button("Save").Variant(ButtonVariant.Primary).HandleClick(_ => onSave()))
            );
  }
}

public class StudentEditSheet(IState<bool> isOpen, Guid studentId, RefreshToken refreshToken, Action onSaveCallback) : ViewBase
{
  public override object? Build()
  {
    var student = StudentService.GetStudents().FirstOrDefault(s => s.ID == studentId);
    if (student == null) return null;

    var name = UseState(student.Name);
    var email = UseState(student.Email);
    var course = UseState(student.Course);
    var grade = UseState(student.Grade);
    var age = UseState(student.Age);
    var ageStr = UseState(student.Age.ToString());

    var onSave = new Action(() =>
    {
      student.Name = name.Value;
      student.Email = email.Value;
      student.Course = course.Value;
      student.Grade = grade.Value;
      student.Age = age.Value;

      StudentService.UpdateStudent(student);
      refreshToken.Refresh();
      onSaveCallback?.Invoke();
      isOpen.Set(false);
    });

    // Sync age string -> int
    this.UseEffect(async () =>
    {
      if (int.TryParse(ageStr.Value, out var val)) age.Set(val);
      return (IDisposable?)null;
    }, [ageStr]);

    return Layout.Vertical().Gap(15).Padding(20).Width(400)
            .Add(Text.H3("Edit Student"))
            .Add(Layout.Vertical().Gap(5).Add("Name").Add(name.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Email").Add(email.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Course").Add(course.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Grade").Add(grade.ToTextInput()))
            .Add(Layout.Vertical().Gap(5).Add("Age").Add(ageStr.ToTextInput()))
             .Add(Layout.Horizontal().Gap(10)
                .Add(new Button("Save").Variant(ButtonVariant.Primary).HandleClick(_ => onSave()).Width(Size.Full()))
            );
  }
}


[App(icon: Icons.Sheet, title: "MiniExcel - View", path: ["Examples", "MiniExcel - View"])]
public class MiniExcelViewApp : ViewBase
{
  public override object? Build()
  {
    var refreshToken = this.UseRefreshToken();

    var students = this.UseState(() => StudentService.GetStudents());

    this.UseEffect(async () =>
    {
      students.Set(StudentService.GetStudents());
      return (IDisposable?)null;
    }, [refreshToken.ToTrigger()]);

    this.UseEffect(async () =>
    {
      void OnDataChanged()
      {
        students.Set(StudentService.GetStudents());
        refreshToken.Refresh();
      }

      StudentService.DataChanged += OnDataChanged;
      return (IDisposable?)new EffectDisposable(() => StudentService.DataChanged -= OnDataChanged);
    }, []);

    return BuildTableViewPage(students, refreshToken);
  }

  private object BuildTableViewPage(IState<List<Student>> students, RefreshToken refreshToken)
  {
    var client = UseService<IClientProvider>();
    var uploadState = this.UseState<FileUpload<byte[]>?>();
    var uploadContext = this.UseUpload(MemoryStreamUploadHandler.Create(uploadState))
        .Accept(".xlsx")
        .MaxFileSize(50 * 1024 * 1024);
    var actionMode = this.UseState("Export");

    var downloadUrl = this.UseDownload(
        async () =>
        {
          await using var ms = new MemoryStream();
          MiniExcel.SaveAs(ms, students.Value);
          return ms.ToArray();
        },
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"students-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.xlsx"
    );

    this.UseEffect(async () =>
    {
      if (uploadState.Value?.Content is byte[] bytes && bytes.Length > 0)
      {
        try
        {
          using var ms = new MemoryStream(bytes);
          var imported = MiniExcel.Query<Student>(ms).ToList();

          var currentStudents = StudentService.GetStudents();
          var studentsById = currentStudents.ToDictionary(s => s.ID);
          foreach (var importedStudent in imported)
          {
            if (importedStudent.ID != Guid.Empty && studentsById.TryGetValue(importedStudent.ID, out var existing))
            {
              existing.Name = importedStudent.Name;
              existing.Email = importedStudent.Email;
              existing.Age = importedStudent.Age;
              existing.Course = importedStudent.Course;
              existing.Grade = importedStudent.Grade;
            }
            else
            {
              if (importedStudent.ID == Guid.Empty)
              {
                importedStudent.ID = Guid.NewGuid();
              }
              currentStudents.Add(importedStudent);
              studentsById[importedStudent.ID] = importedStudent;
            }
          }

          StudentService.UpdateStudents(currentStudents);
          students.Set(StudentService.GetStudents());
          refreshToken.Refresh();
          client.Toast($"Imported {imported.Count} students");
        }
        catch (Exception ex)
        {
          client.Toast($"Import error: {ex.Message}");
        }
        finally
        {
          uploadState.Set((FileUpload<byte[]>?)null);
        }
      }
      return (IDisposable?)null;
    }, [uploadState]);

    object? actionWidget = actionMode.Value == "Export"
        ? (object)new Button("Download Excel File")
            .Icon(Icons.Download)
            .Variant(ButtonVariant.Primary)
            .Url(downloadUrl.Value)
            .Width(Size.Full())
        : (object)uploadState.ToFileInput(uploadContext)
            .Placeholder("Choose File");

    return Layout.Horizontal().Gap(20)
        .Add(new Card(
            Layout.Vertical().Gap(10)
            .Add(Text.H3("Data Management"))
            .Add(Text.Small("Upload and download Excel files with students data"))
            .Add(actionMode.ToSelectInput(new List<string> { "Export", "Import" }.Select(s => new Option<string>(s, s)).ToList()))
            .Add(actionWidget)
            .Add(new Spacer().Height(Size.Units(5)))
            .Add(Text.Small("This demo uses MiniExcel to manage students data."))
            .Add(Text.Markdown("Built with [Ivy Framework](https://github.com/Ivy-Interactive/Ivy-Framework) and [MiniExcel](https://github.com/mini-software/MiniExcel)"))
        ).Width(Size.Fraction(0.4f)))
        .Add(new Card(
            Layout.Vertical()
            .Add(Text.H3("Data Overview"))
            .Add(Text.Small($"Search, filter and view all students data. Total records: {students.Value.Count}"))
            .Add(students.Value.Count > 0
                ? students.Value.AsQueryable().ToDataTable()
                    .Hidden(s => s.ID)
                    .Width(Size.Full())
                    .Height(Size.Units(140))
                    .Key($"students-{students.Value.Count}-{students.Value.Sum(s => s.GetHashCode())}")
                : Layout.Center()
                    .Add(Text.Small("No data to display"))
            )
        ).Height(Size.Fit().Min(Size.Full())));
  }
}
