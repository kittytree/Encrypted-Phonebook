using System;

namespace Phonebook;

public sealed record Contact(string? Name, string? PhoneNumber, string? Email, string? Notes);