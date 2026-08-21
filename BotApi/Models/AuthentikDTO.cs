using System.Text.Json.Serialization;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class Attributes
{
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("sms")]
    public bool? Sms { get; set; }
}

public class AuthentikUserGroupDto
{
    public string pk { get; set; }
    public int num_pk { get; set; }
    public string name { get; set; }
    public bool is_superuser { get; set; }
    public List<string> parents { get; set; }
    public object parents_obj { get; set; }
    public List<int> users { get; set; }
    public List<UsersObj> users_obj { get; set; }
    public Attributes attributes { get; set; }
    public List<object> roles { get; set; }
    public List<object> roles_obj { get; set; }
    public object inherited_roles_obj { get; set; }
    public List<object> children { get; set; }
    public object children_obj { get; set; }
}

public class UsersObj
{
    public int pk { get; set; }
    public string username { get; set; }
    public string name { get; set; }
    public bool is_active { get; set; }
    public object last_login { get; set; }
    public string email { get; set; }
    public Attributes attributes { get; set; }
    public string uid { get; set; }
}

