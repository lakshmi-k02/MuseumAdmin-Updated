// namespace MuseumAdmin.Models
// {
//     public class ApiResponse<T>
//     {
//         public bool status { get; set; }
//         public int statusCode { get; set; }
//         public string message { get; set; }
//         public T data { get; set; }
//     }
// }

namespace MuseumAdmin.Models
{
    public class ApiResponse<T>
    {
        public bool Status { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}

