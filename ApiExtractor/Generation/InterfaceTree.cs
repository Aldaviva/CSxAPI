using System.Collections.Generic;

namespace ApiExtractor.Generation;

public interface InterfaceChild { }

public class InterfaceMethod<T>: InterfaceChild {

    public InterfaceMethod(T command) {
        this.command = command;
    }

    public T command { get; }

    private bool Equals(InterfaceMethod<T> other) {
        return EqualityComparer<T>.Default.Equals(command, other.command);
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((InterfaceMethod<T>) obj);
    }

    public override int GetHashCode() {
        return EqualityComparer<T>.Default.GetHashCode(command!);
    }

    public static bool operator ==(InterfaceMethod<T>? left, InterfaceMethod<T>? right) {
        return Equals(left, right);
    }

    public static bool operator !=(InterfaceMethod<T>? left, InterfaceMethod<T>? right) {
        return !Equals(left, right);
    }

}

public class Subinterface<T>: InterfaceChild {

    public Subinterface(string interfaceName, string getterName) {
        this.interfaceName = interfaceName;
        this.getterName    = getterName;
    }

    public string interfaceName { get; }
    public string getterName { get; }

    private bool Equals(Subinterface<T> other) {
        return interfaceName == other.interfaceName;
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Subinterface<T>) obj);
    }

    public override int GetHashCode() {
        return interfaceName.GetHashCode();
    }

    public static bool operator ==(Subinterface<T>? left, Subinterface<T>? right) {
        return Equals(left, right);
    }

    public static bool operator !=(Subinterface<T>? left, Subinterface<T>? right) {
        return !Equals(left, right);
    }

}