using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Phonebook;

public static class Phonebook
{
    private static readonly JsonSerializerOptions _jsonSerializeOptions = new() { WriteIndented = true };

    public static void Main(string[] args)
    {
        List<string>  registeredAccounts = ReadFromFile<string>(@"C:\Users\kitty\source\repos\Phonebook\Phonebook\registeredAccounts.json");
        (string username ,List<Contact>? userContacts, byte[]? key) = LoginAttempt(registeredAccounts);
        if (userContacts == null || key == null) return;
        AddUsernameToRegisteredList(registeredAccounts, username);
        WriteRegisteredUsernames(registeredAccounts, @"C:\Users\kitty\source\repos\Phonebook\Phonebook\registeredAccounts.json");
        DisplayEntries(userContacts);

        UserDecision decision = UserInput();
        while (decision != UserDecision.Exit)
        {
            switch (decision)
            {
                case UserDecision.Display:
                    DisplayEntries(userContacts);
                    break;

                case UserDecision.Add:
                    userContacts = CreateContact(userContacts);
                    DisplayEntries(userContacts);
                    break;

                case UserDecision.Delete:
                    DeleteContact(userContacts);
                    DisplayEntries(userContacts);
                    break;

                case UserDecision.Edit:
                    EditContact(userContacts);
                    DisplayEntries(userContacts);
                    break;
            }
            decision = UserInput();
        }
        WriteEncryptedAes(userContacts, @$"C:\Users\kitty\source\repos\Phonebook\Phonebook\{username}.json", key);
    }

    private static void WriteRegisteredUsernames(List<string> registeredUsernames, string path)
    {
        File.WriteAllText(path,JsonSerializer.Serialize(registeredUsernames, _jsonSerializeOptions));
    }

    private static void AddUsernameToRegisteredList(List<string> registeredList, string usernameToRegister)
    {
        bool registered = false;
        foreach (string usernameRegistered in registeredList)
        {
            if(usernameRegistered == usernameToRegister)
            {
                registered = true;
                return;
            }
        }
        if (!registered)
        {
            registeredList.Add(usernameToRegister);
        }
        return;
    }

    private static byte[] PasswordToBytes(string password)
    {
        return KeyDerivation.Pbkdf2(password, new byte[128 >> 3], KeyDerivationPrf.HMACSHA512, iterationCount: 10000, 256 >> 3);
    }

    private static List<Contact>? ReadEncryptedAes(string path, byte[] key)
    {
        try
        {
            using FileStream fileStream = new(path, FileMode.Open);
            using Aes aes = Aes.Create();

            byte[] iv = new byte[aes.IV.Length];
            int numBytesToRead = aes.IV.Length;
            int numBytesRead = 0;
            while (numBytesToRead > 0)
            {
                int n = fileStream.Read(iv, numBytesRead, numBytesToRead);
                if (n == 0) break;

                numBytesRead += n;
                numBytesToRead -= n;
            }

            using CryptoStream cryptoStream = new(
               fileStream,
               aes.CreateDecryptor(key, iv),
               CryptoStreamMode.Read);

            return JsonSerializer.Deserialize<List<Contact>>(cryptoStream);

        }
        catch (JsonException ex)
        {
            Console.WriteLine($"The decryption failed. {ex}");
            return null;
        }
    }

    private static void WriteEncryptedAes(List<Contact> userContacts, string path, byte[] key)
    {
        try
        {
            using FileStream fileStream = new(path, FileMode.OpenOrCreate);
            using Aes aes = Aes.Create();  
            aes.Key = key;

            byte[] iv = aes.IV;
            fileStream.Write(iv, 0, iv.Length);

            using CryptoStream cryptoStream = new(
                fileStream,
                aes.CreateEncryptor(),
                CryptoStreamMode.Write);

            JsonSerializer.Serialize<List<Contact>>(cryptoStream, userContacts, _jsonSerializeOptions);     
            Console.WriteLine("The file was encrypted.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"The encryption failed. {ex}");
        }
    }

    private static string? GetUserInputAdd(string request)
    {
        Console.Write(request);
        string? userAnswer = Console.ReadLine();
        return userAnswer;
    }

    private static List<Contact> CreateContact(List<Contact> userContacts)
    {
        string? name = GetUserInputAdd("Please type the name of the contact you wish to add (you may leave this blank): ");
        string? phoneNumber = GetUserInputAdd($"Please type the phone number for {name} (you may leave this blank): ");    
        string? email = GetUserInputAdd($"Please type the email for {name} (you may leave this blank): ");
        string? notes = GetUserInputAdd($"Please type any notes for {name} (you may leave this blank): ");

        userContacts.Add(new Contact(name, phoneNumber, email, notes));
        return userContacts;
        
    }

    private static void DeleteContact(List<Contact> userContacts)
    {
        string? name = GetUserInputAdd("What is the name of the contact you wish to delete?: ");
        foreach (Contact contact in userContacts)
        {
            if(contact.Name == name)
            {
                userContacts.Remove(contact);
                return;
            }
        }
        Console.WriteLine("That contact did not exist");
        return;
    }

    private static Contact? GetContacts(List<Contact> userContacts, string name)
    {
        foreach (Contact contact in userContacts)
        {
            if (contact.Name == name)
            {
                return contact;
            }
        }
        return null;

    }

    private static void EditContact(List<Contact> userContacts)
    {
        string? name = GetUserInputAdd("What is the name of the contact you wish to edit?: ");
        if (string.IsNullOrEmpty(name)) 
        {
            return;
        }

        Contact? toEditContact = GetContacts(userContacts, name);
        if (toEditContact == null)
        {
            return;
        }

        string? contactName = toEditContact.Name;
        string? contactPhoneNumber = toEditContact.PhoneNumber;
        string? contactEmail = toEditContact.Email;
        string? contactNotes = toEditContact.Notes;

        Console.WriteLine($"The contact you wish to edit is: {toEditContact}");
        string? attributeToEdit = GetUserInputAdd("Which attribute do you wish to edit; name, email, phoneNumber, and notes?: ");
        Console.WriteLine(attributeToEdit);
        if (string.IsNullOrWhiteSpace(attributeToEdit))
        {
            Console.WriteLine("Invalid Input");
            return;
        }

        if (attributeToEdit == "name")
        {
            string? newAttributeValue = GetUserInputAdd($"What would you like to change the name {toEditContact.Name} to?: ");
            if (string.IsNullOrWhiteSpace(newAttributeValue))
            {
                Console.WriteLine("Invalid input");
                return;
            }
            contactName = newAttributeValue;

        }
        else if (attributeToEdit == "phoneNumber")
        {
            string? newAttributeValue = GetUserInputAdd($"What would you like to change the name {toEditContact.PhoneNumber} to?: ");
            if (string.IsNullOrWhiteSpace(newAttributeValue))
            {
                Console.WriteLine("Invalid input");
                return;
            }
            contactPhoneNumber = newAttributeValue;
        }
        else if (attributeToEdit == "email")
        {
            string? newAttributeValue = GetUserInputAdd($"What would you like to change the name {toEditContact.Email} to?: ");
            if (string.IsNullOrWhiteSpace(newAttributeValue))
            {
                Console.WriteLine("Invalid input");
                return;
            }
            contactEmail = newAttributeValue;
        }
        else if (attributeToEdit == "notes")
        {
            string? newAttributeValue = GetUserInputAdd($"What would you like to change the name {toEditContact.Notes} to?: ");
            if (string.IsNullOrWhiteSpace(newAttributeValue))
            {
                Console.WriteLine("Invalid input");
                return;
            }
            contactNotes = newAttributeValue;
        }
        else
        {
            return;
        }
        userContacts.Remove(toEditContact);
        userContacts.Add(new Contact(contactName, contactPhoneNumber, contactEmail, contactNotes));
        return;
    }

    private static UserDecision UserInput()
    {
        Console.WriteLine("Type 1 to display contacts");
        Console.WriteLine("Type 2 to add a contact");
        Console.WriteLine("Type 3 to delete a contact");
        Console.WriteLine("Type 4 to edit a contact");
        Console.WriteLine("Type 5 to exit the program");
        
        return Console.ReadLine() switch
        {
            "1" => UserDecision.Display,
            "2" => UserDecision.Add,
            "3" => UserDecision.Delete,
            "4" => UserDecision.Edit,
            "5" => UserDecision.Exit,
            _   => throw new Exception()
        };

    }

    private static void DisplayEntries(List<Contact> contacts)
    {
        foreach (Contact contact in contacts)
        {
            Console.WriteLine(contact.ToString());
        }
    }

    private static List<T> ReadFromFile<T>(string path)
    {
        if (!File.Exists(path))
        {
            // Create a file to write to.
            File.Create(path)
                .Dispose();
            return new List<T>();
        }

        string jsonString = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(jsonString)!;
    }

    private static (string, List<Contact>?, byte[]?) LoginAttempt(List<string> registeredAccounts)
    {
        Console.WriteLine("Please select the username");
        string? username;
        while (string.IsNullOrWhiteSpace(username = Console.ReadLine()))
        {
            Console.WriteLine("Incorrect Login, please use a non empty username:");
        }
        
        bool accountIsRegistered = false;
        int loginAttempts = 0;

        foreach (string account in registeredAccounts) 
        {
            if (account == username)
            {
                accountIsRegistered = true;
                Console.WriteLine("Please type the password to unlock the account.: ");
                string? attemptedPassword;
                while(string.IsNullOrWhiteSpace(attemptedPassword = Console.ReadLine()))
                {
                    Console.WriteLine("No empty passwords allowed");
                }
                byte[] bytedPassword = PasswordToBytes(attemptedPassword);
                while (loginAttempts < 3)
                {
                    List<Contact>? contacts = ReadEncryptedAes(@$"C:\Users\kitty\source\repos\Phonebook\Phonebook\{username}.json", bytedPassword);
                    if (contacts == null)
                    {
                        Console.WriteLine("Password rejected");
                        loginAttempts++;
                        Console.WriteLine("Try again...\n Please type the password to unlock the account.: ");
                        while (string.IsNullOrWhiteSpace(attemptedPassword = Console.ReadLine()))
                        {
                            Console.WriteLine("No empty passwords allowed");
                        }
                        bytedPassword = PasswordToBytes(attemptedPassword);
                    }
                    else
                    {
                        return (username, contacts, bytedPassword);
                    }
                }
            }
        }
        
        if (!accountIsRegistered)
        {
            Console.WriteLine("Would you like to create an account yes, or no?");
            string? accountCreationDecision = GetUserInputAdd(" Please type 'y' to create an account or 'n' to exit ");
            if (string.IsNullOrWhiteSpace(accountCreationDecision) || "n" == accountCreationDecision)
            {
                return (username, null, null);
            }

            Console.WriteLine("Please type a password");
            string? attemptedPasswordCreation = Console.ReadLine();
            int passwordCreationAttempts = 0;
            while(string.IsNullOrWhiteSpace(attemptedPasswordCreation) && passwordCreationAttempts < 3) 
            {
                attemptedPasswordCreation = GetUserInputAdd("Please type a non blank password");
                passwordCreationAttempts++;
            }
            if(passwordCreationAttempts >= 3)
            {
                Console.WriteLine("You've exceeded the max number of attempts");
                return (username, null, null);
            }
            else
            {
                if(string.IsNullOrWhiteSpace(attemptedPasswordCreation))
                {
                    return (username, null, null);
                }
                return new (username,new List<Contact>(), PasswordToBytes(attemptedPasswordCreation));
            }
        }
        return (username, null, null);
    }
}
