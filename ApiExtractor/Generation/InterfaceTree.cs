using System.Collections.Generic;

namespace ApiExtractor.Generation;

public interface INterfaceChild { }

public class InterfaceMethod<T>: INterfaceChild {

    public InterfaceMethod(T command) {
        Command = command;
    }

    public T Command { get; }

    private bool Equals(InterfaceMethod<T> other) {
        return EqualityComparer<T>.Default.Equals(Command, other.Command);
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((InterfaceMethod<T>) obj);
    }

    public override int GetHashCode() {
        return EqualityComparer<T>.Default.GetHashCode(Command!);
    }

    public static bool operator ==(InterfaceMethod<T>? left, InterfaceMethod<T>? right) {
        return Equals(left, right);
    }

    public static bool operator !=(InterfaceMethod<T>? left, InterfaceMethod<T>? right) {
        return !Equals(left, right);
    }

}

public class Subinterface<T>: INterfaceChild {

    public Subinterface(string interfaceName, string getterName) {
        InterfaceName = interfaceName;
        GetterName    = getterName;
    }

    public string InterfaceName { get; }
    public string GetterName { get; }

    private bool Equals(Subinterface<T> other) {
        return InterfaceName == other.InterfaceName;
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Subinterface<T>) obj);
    }

    public override int GetHashCode() {
        return InterfaceName.GetHashCode();
    }

    public static bool operator ==(Subinterface<T>? left, Subinterface<T>? right) {
        return Equals(left, right);
    }

    public static bool operator !=(Subinterface<T>? left, Subinterface<T>? right) {
        return !Equals(left, right);
    }

}