using System;

namespace MeshDeleter.Models
{
    public class NotFoundVerticesException : Exception
    {
        public NotFoundVerticesException() : base("Vertices Not Found") { }

        public NotFoundVerticesException(string message) : base(message){}
    }
} 