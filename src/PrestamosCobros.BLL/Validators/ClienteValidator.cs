using FluentValidation;
using PrestamosCobros.BLL.DTOs;

namespace PrestamosCobros.BLL.Validators;

public class ClienteCreateDtoValidator : AbstractValidator<ClienteCreateDto>
{
    public ClienteCreateDtoValidator()
    {
        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres");

        RuleFor(x => x.Telefono)
            .NotEmpty().WithMessage("El teléfono es requerido")
            .MaximumLength(20).WithMessage("El teléfono no puede exceder 20 caracteres");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email no válido")
            .MaximumLength(150).When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Cedula)
            .MaximumLength(30).WithMessage("La cédula no puede exceder 30 caracteres");

        RuleFor(x => x.Direccion)
            .MaximumLength(300).WithMessage("La dirección no puede exceder 300 caracteres");

        RuleFor(x => x.LugarTrabajo)
            .MaximumLength(200).WithMessage("El lugar de trabajo no puede exceder 200 caracteres");
    }
}

public class ClienteEditDtoValidator : AbstractValidator<ClienteEditDto>
{
    public ClienteEditDtoValidator()
    {
        Include(new ClienteCreateDtoValidator());
        RuleFor(x => x.ClienteId).GreaterThan(0).WithMessage("ID de cliente inválido");
    }
}
