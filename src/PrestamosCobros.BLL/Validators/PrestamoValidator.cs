using FluentValidation;
using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Validators;

public class PrestamoCreateDtoValidator : AbstractValidator<PrestamoCreateDto>
{
    public PrestamoCreateDtoValidator()
    {
        RuleFor(x => x.ClienteId)
            .GreaterThan(0).WithMessage("El cliente es requerido");

        RuleFor(x => x.NombreAlias)
            .MaximumLength(100).WithMessage("El alias no puede exceder 100 caracteres");

        RuleFor(x => x.MontoPrestado)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a 0");

        RuleFor(x => x.PorcentajeInteres)
            .InclusiveBetween(0, 100).WithMessage("El interés debe estar entre 0 y 100");

        RuleFor(x => x.FechaInicio)
            .NotEmpty().WithMessage("La fecha de inicio es requerida");

        RuleFor(x => x.PeriodicidadDias)
            .InclusiveBetween(1, 365).WithMessage("La periodicidad debe estar entre 1 y 365 días");

        RuleFor(x => x.NumeroCuotas)
            .InclusiveBetween(1, 120).WithMessage("El número de cuotas debe estar entre 1 y 120");

        RuleFor(x => x.NumeroSinpe)
            .MaximumLength(30).WithMessage("El número SINPE no puede exceder 30 caracteres");

        RuleFor(x => x.CuentasBancarias)
            .MaximumLength(500).WithMessage("Las cuentas bancarias no pueden exceder 500 caracteres");
    }
}
