using MongoDB.Bson.Serialization.Attributes;
using ProtoBuf;

namespace Benchmark
{
    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    public class Cat : Animal
    {
        public string Vaccinations { get; set; }
    }

    public class Dog : Animal
    {
        public string Vaccinations { get; set; }
    }

    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(Cat), typeof(Dog))]
    public class Animal
    {
        public string Name { get; set; }
        public string Info { get; set; }
    }

    public class Townhouse : REProperty
    {
        public string Notes { get; set; }
        public int Floors { get; set; }
    }

    public class Condo : REProperty
    {
        public string Notes { get; set; }
        public int FloorNum { get; set; }
    }

    public class CondoNested1 : Condo
    {
    }

    public class CondoNested2 : Condo
    {
    }

    public class CondoNested3 : Condo
    {
    }

    public class CondoNested4 : Condo
    {
    }
    public class CondoNested5 : Condo
    {
    }
    public class CondoNested6 : Condo
    {
    }

    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(Townhouse), typeof(Condo), typeof(CondoNested1), typeof(CondoNested2), typeof(CondoNested3), typeof(CondoNested4), typeof(CondoNested5), typeof(CondoNested6))]
    public class REProperty
    {
        public string Name { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Person
    {
        public int Age { get; set; }
        public Address Address { get; set; }

        public string Name { get; set; }
        public string LastName { get; set; }
        public string LastName2 { get; set; }
        public string LastName3 { get; set; }
        public string LastName4 { get; set; }
        public string LastName5 { get; set; }

        //public double[] Readings { get; set; }
        public int[] Numbers { get; set; }
        public Animal[] Pets { get; set; }
        public REProperty[] Properties { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PersonCopy
    {
        public int Age { get; set; }
        public Address Address { get; set; }

        public string Name { get; set; }
        public string LastName { get; set; }
        public string LastName2 { get; set; }
        public string LastName3 { get; set; }
        public string LastName4 { get; set; }
        public string LastName5 { get; set; }

        //public double[] Readings { get; set; }
        public int[] Numbers { get; set; }
        public Animal[] Pets { get; set; }
        public REProperty[] Properties { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PersonCopyNoId
    {
        public int Age { get; set; }
        public Address Address { get; set; }

        public string Name { get; set; }
        public string LastName { get; set; }
        public string LastName2 { get; set; }
        public string LastName3 { get; set; }
        public string LastName4 { get; set; }
        public string LastName5 { get; set; }

        //public double[] Readings { get; set; }
        public int[] Numbers { get; set; }
        public Animal[] Pets { get; set; }
        public REProperty[] Properties { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PatientData
    {
        public int Age { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }

        public double Weight { get; set; }
        public double Temperature { get; set; }
        public string Comments { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PatientDataCopy
    {
        public int Age { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }

        public double Weight { get; set; }
        public double Temperature { get; set; }
        public string Comments { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PatientDataCopyNoId
    {
        public int Age { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }

        public double Weight { get; set; }
        public double Temperature { get; set; }
        public string Comments { get; set; }
    }

    [ProtoContract]
    public class PatientDataProtobuf
    {
        [ProtoMember(1)]
        public int Age { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public string LastName { get; set; }
        [ProtoMember(4)]
        public string PhoneNumber { get; set; }

        [ProtoMember(5)]
        public double Weight { get; set; }
        [ProtoMember(6)]
        public double Temperature { get; set; }
    }
}
